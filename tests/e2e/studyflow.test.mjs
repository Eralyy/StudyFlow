import assert from 'node:assert/strict';
import { after, afterEach, before, beforeEach, test } from 'node:test';
import { spawn, execFile } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { createConnection } from 'node:net';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { setTimeout as delay } from 'node:timers/promises';

const require = createRequire(import.meta.url);
const { chromium } = require(process.env.STUDYFLOW_PLAYWRIGHT_MODULE || 'playwright');
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const port = Number(process.env.STUDYFLOW_E2E_PORT || 5191);
const address = `http://127.0.0.1:${port}`;
let browser, context, page, server, temporaryDirectory;
let serverLog = '', serverError, pageErrors = [];

async function portIsListening() {
  return await new Promise((resolvePort, reject) => {
    const probe = createConnection({ port, host: '127.0.0.1' });
    probe.once('connect', () => { probe.destroy(); resolvePort(true); });
    probe.once('error', error => error.code === 'ECONNREFUSED' ? resolvePort(false) : reject(error));
    probe.setTimeout(2000, () => { probe.destroy(); reject(new Error(`Could not check port ${port}.`)); });
  });
}

before(async () => {
  const options = { headless: true };
  if (process.env.STUDYFLOW_BROWSER_CHANNEL) options.channel = process.env.STUDYFLOW_BROWSER_CHANNEL;
  try {
    browser = await chromium.launch(options);
  } catch (error) {
    if (process.platform !== 'win32' || options.channel || !error.message.includes("Executable doesn't exist")) throw error;
    browser = await chromium.launch({ headless: true, channel: 'chrome' });
  }
});

beforeEach(async () => {
  // Refuse an occupied port so the tests can never change another running workspace.
  assert.equal(await portIsListening(), false, `Port ${port} is already occupied.`);
  temporaryDirectory = await mkdtemp(join(tmpdir(), 'studyflow-e2e-'));
  serverLog = ''; serverError = undefined; pageErrors = [];
  server = spawn(process.env.DOTNET_EXE || 'dotnet', [
    'run', '--no-build', '--project', 'src/StudyFlow.Web', '--no-launch-profile', '--urls', address
  ], {
    cwd: root,
    windowsHide: true,
    detached: process.platform !== 'win32',
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      ConnectionStrings__StudyFlow: `Data Source=${join(temporaryDirectory, 'studyflow.db')};Pooling=False`
    },
    stdio: ['ignore', 'pipe', 'pipe']
  });
  server.on('error', error => { serverError = error; });
  const keepRecentLog = data => { serverLog = (serverLog + data).slice(-12000); };
  server.stdout.on('data', keepRecentLog);
  server.stderr.on('data', keepRecentLog);
  const deadline = Date.now() + 30000;
  while (true) {
    if (serverError) throw serverError;
    if (server.exitCode !== null) throw new Error(`StudyFlow stopped during startup:\n${serverLog}`);
    let response;
    try { response = await fetch(address); } catch { /* The local server is still starting. */ }
    if (response?.ok) break;
    if (response?.status >= 500) throw new Error(`StudyFlow returned HTTP ${response.status}:\n${serverLog}`);
    if (Date.now() > deadline) throw new Error(`StudyFlow did not start:\n${serverLog}`);
    await delay(100);
  }
  context = await browser.newContext({ baseURL: address, viewport: { width: 1440, height: 1000 } });
  page = await context.newPage();
  page.on('pageerror', error => pageErrors.push(error.message));
  await page.goto('/', { waitUntil: 'networkidle' });
});

afterEach(async () => {
  await context?.close(); context = undefined;
  if (server?.pid && server.exitCode === null) {
    if (process.platform === 'win32') {
      await promisify(execFile)('taskkill', ['/PID', String(server.pid), '/T', '/F'], { windowsHide: true });
    } else {
      process.kill(-server.pid, 'SIGTERM');
      for (let attempt = 0; attempt < 30 && server.exitCode === null; attempt++) await delay(100);
    }
    const shutdownDeadline = Date.now() + 5000;
    while (await portIsListening()) {
      if (Date.now() > shutdownDeadline) throw new Error(`The test server did not release port ${port}.`);
      await delay(50);
    }
  }
  server = undefined;
  if (temporaryDirectory) {
    const target = resolve(temporaryDirectory);
    assert.equal(dirname(target), resolve(tmpdir()));
    assert.ok(target.startsWith(join(resolve(tmpdir()), 'studyflow-e2e-')));
    await rm(target, { recursive: true, force: true, maxRetries: 5, retryDelay: 100 });
    temporaryDirectory = undefined;
  }
  assert.deepEqual(pageErrors, [], 'The browser must not report unhandled JavaScript errors');
});

after(async () => { await browser?.close(); });

async function openSubjects() {
  await page.getByRole('link', { name: 'Subjects', exact: true }).click();
  await page.locator('#subject-name').waitFor();
}

async function addSubject(name = 'Programming') {
  await openSubjects();
  await page.locator('#subject-name').fill(name);
  await page.getByRole('button', { name: 'Add subject', exact: true }).click();
  await page.getByRole('heading', { name, exact: true }).waitFor();
}

async function openTasks() {
  await page.getByRole('link', { name: 'My tasks', exact: true }).click();
  await page.getByRole('heading', { name: /^My tasks/ }).waitFor();
}

async function newTask(title, subject = 'Programming') {
  await openTasks();
  await page.getByRole('link', { name: 'New task', exact: true }).click();
  await page.locator('#task-title').fill(title);
  await page.locator('#task-subject').selectOption({ label: subject });
  await page.locator('#task-minutes').fill('90');
  await page.locator('#task-priority').selectOption('High');
  await page.locator('#task-notes').fill('Prepare examples for the defence');
  await page.getByRole('button', { name: 'Save task', exact: true }).click();
  await page.getByRole('link', { name: title, exact: true }).waitFor();
}

async function taskCount(expected) {
  const deadline = Date.now() + 5000;
  while (await page.locator('.task-card').count() !== expected && Date.now() < deadline) await delay(50);
  assert.equal(await page.locator('.task-card').count(), expected);
}

async function addSamples() {
  await page.getByRole('button', { name: 'Try sample tasks', exact: true }).click();
  await taskCount(5);
}

async function navigateWithConfirmation(linkName, leave) {
  const dialogOpened = page.waitForEvent('dialog');
  const click = page.getByRole('link', { name: linkName, exact: true }).click();
  const dialog = await dialogOpened;
  assert.equal(dialog.type(), 'confirm');
  if (leave) await dialog.accept(); else await dialog.dismiss();
  await click;
}

test('create, edit and complete a task; changes survive a page reload', { timeout: 60000 }, async () => {
  await addSubject();
  await newTask('Prepare the C# defence');
  await taskCount(1);
  await page.getByRole('button', { name: 'Complete Prepare the C# defence', exact: true }).click();
  await page.getByRole('button', { name: 'Reopen Prepare the C# defence', exact: true }).waitFor();
  await page.getByRole('link', { name: 'Edit Prepare the C# defence', exact: true }).click();
  await page.locator('#task-title').fill('Prepare the final defence');
  await page.locator('#task-minutes').fill('120');
  await page.getByRole('button', { name: 'Save task', exact: true }).click();
  await page.getByRole('button', { name: 'Reopen Prepare the final defence', exact: true }).waitFor();
  await page.reload({ waitUntil: 'networkidle' });
  await taskCount(1);
  assert.match(await page.locator('.task-card').innerText(), /Prepare the final defence/);
  assert.match(await page.locator('.task-meta').innerText(), /2h/);
  assert.equal(await page.getByRole('button', { name: 'Reopen Prepare the final defence', exact: true }).count(), 1);
  await page.getByRole('button', { name: 'Reopen Prepare the final defence', exact: true }).click();
  await page.getByRole('button', { name: 'Complete Prepare the final defence', exact: true }).waitFor();
});

test('invalid subject and task inputs show feedback without saving records', { timeout: 60000 }, async () => {
  await openSubjects();
  await page.getByRole('button', { name: 'Add subject', exact: true }).click();
  await page.locator('.validation-message').first().waitFor();
  assert.equal(await page.locator('.subject-card').count(), 0);
  await addSubject();
  await openTasks();
  await page.getByRole('link', { name: 'New task', exact: true }).click();
  await page.getByRole('button', { name: 'Save task', exact: true }).click();
  await page.getByText('Enter a task title.', { exact: true }).waitFor();
  await page.locator('#task-title').fill('Invalid estimate');
  await page.locator('#task-minutes').fill('10');
  await page.locator('#task-minutes').press('Tab');
  await page.getByText('Estimated time must be between 15 and 1440 minutes.', { exact: true }).waitFor();
  assert.ok(page.url().endsWith('/tasks/new'));
  await navigateWithConfirmation('My tasks', true);
  await page.getByRole('heading', { name: /^My tasks/ }).waitFor();
  await taskCount(0);
});

test('delete confirmations protect tasks and occupied subjects', { timeout: 60000 }, async () => {
  await addSubject();
  await newTask('Keep my coursework');
  await openSubjects();
  await page.getByRole('button', { name: 'Delete Programming', exact: true }).click();
  await page.getByRole('button', { name: 'Keep subject', exact: true }).click();
  assert.equal(await page.locator('.subject-card').count(), 1);
  await page.getByRole('button', { name: 'Delete Programming', exact: true }).click();
  await page.getByRole('button', { name: 'Delete subject', exact: true }).click();
  await page.getByText('This subject has tasks. Delete or move its tasks first.', { exact: true }).waitFor();
  assert.equal(await page.locator('.subject-card').count(), 1);
  await openTasks();
  await page.getByRole('button', { name: 'Delete Keep my coursework', exact: true }).click();
  await page.getByRole('button', { name: 'Keep task', exact: true }).click();
  await taskCount(1);
  await page.getByRole('button', { name: 'Delete Keep my coursework', exact: true }).click();
  await page.getByRole('button', { name: 'Delete task', exact: true }).click();
  await taskCount(0);
  await openSubjects();
  await page.getByRole('button', { name: 'Delete Programming', exact: true }).click();
  await page.getByRole('button', { name: 'Delete subject', exact: true }).click();
  await page.locator('.subject-card').waitFor({ state: 'detached' });
});

test('search, subject, status and sorting controls show the correct tasks', { timeout: 60000 }, async () => {
  await addSamples();
  await page.getByRole('button', { name: 'Overdue', exact: true }).click();
  await taskCount(1);
  assert.match(await page.locator('.task-card').innerText(), /Read the chapter on pure functions/);
  await page.getByRole('button', { name: 'Completed', exact: true }).click();
  await page.getByRole('link', { name: 'Sketch the page layout', exact: true }).waitFor();
  await taskCount(1);
  await page.getByRole('button', { name: 'All', exact: true }).click();
  await page.locator('#task-search').fill('COLLECTION');
  await page.getByRole('link', { name: 'Complete the LINQ practice', exact: true }).waitFor();
  await taskCount(1);
  await page.locator('#task-search').fill('');
  await page.locator('#subject-filter').selectOption({ label: 'Functional Programming' });
  await taskCount(2);
  await page.locator('#subject-filter').selectOption('');
  await page.locator('#task-sort').selectOption('Priority');
  await taskCount(5);
  await page.locator('.task-card').nth(1).locator('.priority').filter({ hasText: 'High priority' }).waitFor();
  assert.deepEqual(await page.locator('.priority').allTextContents(), [
    'High priority', 'High priority', 'Medium priority', 'Low priority', 'Low priority'
  ]);
  await page.locator('#task-search').fill('no such coursework');
  await page.getByText('No tasks match these filters.', { exact: true }).waitFor();
  await taskCount(0);
  await page.getByRole('button', { name: 'Clear filters', exact: true }).click();
  await taskCount(5);
});

test('sample data persists and can only be added when the task list is empty', { timeout: 60000 }, async () => {
  await addSamples();
  assert.equal(await page.getByRole('button', { name: 'Try sample tasks', exact: true }).count(), 0);
  await page.reload({ waitUntil: 'networkidle' });
  await taskCount(5);
  for (let remaining = 5; remaining > 0; remaining--) {
    await page.locator('.task-card').first().getByRole('button', { name: /^Delete / }).click();
    await page.getByRole('button', { name: 'Delete task', exact: true }).click();
    await taskCount(remaining - 1);
  }
  await addSamples();
  await openSubjects();
  assert.equal(await page.locator('.subject-card').count(), 3);
});

test('unsaved edits require confirmation and pages fit a narrow screen', { timeout: 60000 }, async () => {
  await addSubject();
  await openTasks();
  await page.getByRole('link', { name: 'New task', exact: true }).click();
  await page.locator('#task-title').fill('Unsaved draft');
  await page.locator('#task-title').press('Tab');
  await navigateWithConfirmation('Subjects', false);
  assert.ok(page.url().endsWith('/tasks/new'));
  assert.equal(await page.locator('#task-title').inputValue(), 'Unsaved draft');
  await navigateWithConfirmation('Subjects', true);
  await page.locator('#subject-name').waitFor();
  await page.locator('#subject-name').fill('Unsaved subject');
  await page.locator('#subject-name').press('Tab');
  await navigateWithConfirmation('My tasks', false);
  assert.ok(page.url().endsWith('/subjects'));
  await navigateWithConfirmation('My tasks', true);
  await page.getByRole('heading', { name: /^My tasks/ }).waitFor();
  await taskCount(0);
  await openSubjects();
  await page.getByRole('button', { name: 'Edit Programming', exact: true }).click();
  const originalColour = await page.locator('#subject-colour').inputValue();
  await page.locator('#subject-colour').evaluate(input => {
    input.value = '#123456';
    input.dispatchEvent(new Event('change', { bubbles: true }));
  });
  await page.getByText('#123456', { exact: true }).waitFor();
  await navigateWithConfirmation('My tasks', false);
  assert.ok(page.url().endsWith('/subjects'));
  assert.equal(await page.locator('#subject-colour').inputValue(), '#123456');
  await navigateWithConfirmation('My tasks', true);
  await page.getByRole('heading', { name: /^My tasks/ }).waitFor();
  await openSubjects();
  await page.getByRole('button', { name: 'Edit Programming', exact: true }).click();
  assert.equal(await page.locator('#subject-colour').inputValue(), originalColour);
  await openTasks();
  await addSamples();
  await page.setViewportSize({ width: 390, height: 844 });
  for (const path of ['/', '/subjects', '/tasks/new']) {
    await page.goto(path, { waitUntil: 'networkidle' });
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), `${path} overflows horizontally`);
  }
});
