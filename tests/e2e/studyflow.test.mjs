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
const playwright = require(process.env.STUDYFLOW_PLAYWRIGHT_MODULE || 'playwright');
const browserName = process.env.STUDYFLOW_BROWSER || 'chromium';
if (!['chromium', 'firefox', 'webkit'].includes(browserName))
  throw new Error('STUDYFLOW_BROWSER must be chromium, firefox, or webkit.');
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
  if (browserName === 'chromium' && process.env.STUDYFLOW_BROWSER_CHANNEL)
    options.channel = process.env.STUDYFLOW_BROWSER_CHANNEL;
  try {
    browser = await playwright[browserName].launch(options);
  } catch (error) {
    if (browserName !== 'chromium' || process.platform !== 'win32' || options.channel || !error.message.includes("Executable doesn't exist")) throw error;
    browser = await playwright.chromium.launch({ headless: true, channel: 'chrome' });
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
  if (new URL(page.url()).pathname !== '/subjects')
    await page.getByRole('link', { name: 'Subjects', exact: true }).click();
  await page.waitForURL('**/subjects');
  await page.locator('#subject-name:enabled').waitFor();
}

async function addSubject(name = 'Programming') {
  try {
    await openSubjects();
    await fillAndCommit('#subject-name', name);
    await page.getByRole('button', { name: 'Add subject', exact: true }).click();
    await page.getByRole('heading', { name, exact: true }).waitFor();
    await page.locator('#subject-name:enabled').waitFor();
  } catch (error) {
    await explainFailure(error, `Create subject ${name}`);
  }
}

async function openTasks() {
  if (!['/', '/tasks'].includes(new URL(page.url()).pathname))
    await page.getByRole('link', { name: 'My tasks', exact: true }).click();
  await page.waitForURL(url => ['/', '/tasks'].includes(url.pathname));
  await page.locator('.task-section').waitFor();
}

async function newTask(title, subject = 'Programming') {
  try {
    await openTasks();
    await page.getByRole('link', { name: 'New task', exact: true }).click();
    await page.waitForURL('**/tasks/new');
    await fillAndCommit('#task-title', title);
    await selectAndCommit('#task-subject', { label: subject });
    await fillAndCommit('#task-minutes', '90');
    await selectAndCommit('#task-priority', 'High');
    await fillAndCommit('#task-notes', 'Prepare examples for the defence');
    await page.getByRole('button', { name: 'Save task', exact: true }).click();
    await page.getByRole('link', { name: title, exact: true }).waitFor();
  } catch (error) {
    await explainFailure(error, `Create task ${title}`);
  }
}

async function fillAndCommit(selector, value, valid = true) {
  const field = page.locator(selector);
  await field.fill(value);
  // InputText/InputNumber bind on change. Blurring and waiting for Blazor's
  // validation render prevents a disappearing error from moving Save mid-click.
  await field.press('Tab');
  await page.locator(`${selector}.modified.${valid ? 'valid' : 'invalid'}`).waitFor();
  assert.equal(await field.inputValue(), value);
}

async function selectAndCommit(selector, option) {
  const field = page.locator(selector);
  const previous = await field.inputValue();
  const [selected] = await field.selectOption(option);
  if (selected !== previous) await page.locator(`${selector}.modified.valid`).waitFor();
  assert.equal(await field.inputValue(), selected);
}

async function explainFailure(error, operation) {
  let state;
  try {
    state = await page.evaluate(() => ({
      url: location.href,
      fields: [...document.querySelectorAll('form input, form select, form textarea')].map(field => ({
        id: field.id, value: field.value, class: field.className, disabled: field.matches(':disabled'),
        validation: field.validationMessage
      })),
      notices: [...document.querySelectorAll('.validation-message, .notice')].map(item => item.textContent),
      buttons: [...document.querySelectorAll('form button')].map(button => ({ text: button.textContent, disabled: button.matches(':disabled') })),
      pageText: document.querySelector('main')?.innerText.slice(0, 4000)
    }));
  } catch (diagnosticError) {
    state = { diagnosticError: diagnosticError.message };
  }
  throw new Error(`${operation} failed.\nBrowser state:\n${JSON.stringify(state, null, 2)}\nBrowser errors: ${pageErrors.join('; ')}\nRecent server log:\n${serverLog.slice(-6000)}`, { cause: error });
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
  await fillAndCommit('#task-title', 'Prepare the final defence');
  await fillAndCommit('#task-minutes', '120');
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
  await fillAndCommit('#task-title', 'Invalid estimate');
  await fillAndCommit('#task-minutes', '10', false);
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
  await fillAndCommit('#task-title', 'Unsaved draft');
  await navigateWithConfirmation('Subjects', false);
  assert.ok(page.url().endsWith('/tasks/new'));
  assert.equal(await page.locator('#task-title').inputValue(), 'Unsaved draft');
  await navigateWithConfirmation('Subjects', true);
  await page.locator('#subject-name').waitFor();
  await fillAndCommit('#subject-name', 'Unsaved subject');
  await navigateWithConfirmation('My tasks', false);
  assert.ok(page.url().endsWith('/subjects'));
  await navigateWithConfirmation('My tasks', true);
  await page.getByRole('heading', { name: /^My tasks/ }).waitFor();
  await taskCount(0);
  await openSubjects();
  await page.getByRole('button', { name: 'Edit Programming', exact: true }).click();
  // Blazor replaces the keyed form after the server handles Edit.
  await page.getByRole('heading', { name: 'Edit subject', exact: true }).waitFor();
  await page.waitForFunction(() => document.querySelector('#subject-name')?.value === 'Programming');
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
  await page.getByRole('heading', { name: 'Edit subject', exact: true }).waitFor();
  await page.waitForFunction(() => document.querySelector('#subject-name')?.value === 'Programming');
  assert.equal(await page.locator('#subject-colour').inputValue(), originalColour);
  await openTasks();
  await addSamples();
  await page.setViewportSize({ width: 390, height: 844 });
  for (const path of ['/', '/subjects', '/tasks/new', '/planner', '/progress']) {
    await page.goto(path, { waitUntil: 'networkidle' });
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), `${path} overflows horizontally`);
  }
});

async function openPlanner() {
  await page.getByRole('link', { name: 'Planner', exact: true }).click();
  await page.locator('#minutes-Monday').waitFor();
}

async function setDailyBudget(minutes) {
  for (const day of ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'])
    await page.locator(`#minutes-${day}`).fill(String(minutes));
  await page.locator('#minutes-Sunday').press('Tab');
  await page.locator('.unsaved-hint').waitFor();
  await page.getByRole('button', { name: 'Save availability', exact: true }).click();
  await page.locator('.unsaved-hint').waitFor({ state: 'hidden' });
  await page.getByText('Availability saved. Your plan is updated.', { exact: true }).waitFor();
}

test('availability is validated and persisted; plans respect budgets and show unscheduled work', { timeout: 60000 }, async () => {
  await addSamples();
  await openPlanner();
  await page.locator('#minutes-Monday').fill('721');
  await page.locator('#minutes-Monday').press('Tab');
  await page.getByText('Use between 0 and 720 minutes.', { exact: true }).waitFor();
  await setDailyBudget(0);
  assert.equal(await page.locator('.day-plan-card').count(), 14);
  assert.equal(await page.locator('.day-allocations li').count(), 0);
  assert.equal(await page.locator('.unplanned-panel li').count(), 4);
  assert.match(await page.locator('.unplanned-panel').innerText(), /Deadline is before the plan starts/);
  assert.match(await page.locator('.unplanned-panel').innerText(), /Not enough available time/);
  await page.reload({ waitUntil: 'networkidle' });
  assert.equal(await page.locator('#minutes-Monday').inputValue(), '0');
  assert.equal(await page.locator('#minutes-Sunday').inputValue(), '0');
  await setDailyBudget(60);
  assert.equal(await page.locator('.unplanned-panel li').count(), 1);
  assert.match(await page.locator('.unplanned-panel').innerText(), /Read the chapter on pure functions/);
  assert.equal(await page.locator('.day-allocations a').filter({ hasText: 'Sketch the page layout' }).count(), 0);
  const budgets = await page.locator('.day-plan-card progress').evaluateAll(bars => bars.map(bar => ({ value: bar.value, max: bar.max })));
  assert.equal(budgets.reduce((total, bar) => total + bar.value, 0), 255);
  assert.ok(budgets.every(bar => bar.value <= 60 && bar.value <= bar.max));
  const linqDays = await page.locator('.day-plan-card').filter({ has: page.getByRole('link', { name: 'Complete the LINQ practice', exact: true }) }).count();
  assert.equal(linqDays, 2, 'The 90-minute task should span two 60-minute days');
  await page.locator('#plan-days').selectOption('7');
  await page.getByRole('button', { name: 'Generate plan', exact: true }).click();
  await page.waitForFunction(() => document.querySelectorAll('.day-plan-card').length === 7);
  await page.locator('#plan-days').selectOption('30');
  await page.getByRole('button', { name: 'Generate plan', exact: true }).click();
  await page.waitForFunction(() => document.querySelectorAll('.day-plan-card').length === 30);
});

test('progress reflects completed tasks, subject totals and remaining estimates', { timeout: 60000 }, async () => {
  await addSamples();
  await page.getByRole('link', { name: 'Progress', exact: true }).click();
  await page.locator('[data-stat="open"]').waitFor();
  assert.equal(await page.locator('[data-stat="open"]').innerText(), '4');
  assert.equal(await page.locator('[data-stat="completed"]').innerText(), '1');
  assert.equal(await page.locator('[data-stat="overdue"]').innerText(), '1');
  assert.equal(await page.locator('.completion-percent').innerText(), '20%');
  assert.match(await page.locator('.estimate-summary').innerText(), /5h 15m/);
  assert.equal(await page.locator('.subject-progress-card').count(), 3);
  assert.equal(await page.locator('.deadline-list li').count(), 3);
  await openTasks();
  await page.getByRole('button', { name: 'Complete Complete the LINQ practice', exact: true }).click();
  await page.getByRole('button', { name: 'Reopen Complete the LINQ practice', exact: true }).waitFor();
  await page.getByRole('link', { name: 'Progress', exact: true }).click();
  await page.getByText('40%', { exact: true }).waitFor();
  assert.equal(await page.locator('[data-stat="completed"]').innerText(), '2');
  assert.match(await page.locator('.estimate-summary').innerText(), /3h 45m/);
  const programming = page.locator('.subject-progress-card').filter({ hasText: 'Functional Programming' });
  assert.match(await programming.innerText(), /50%/);
  await page.reload({ waitUntil: 'networkidle' });
  assert.equal(await page.locator('.completion-percent').innerText(), '40%');
  await openPlanner();
  assert.equal(await page.locator('.day-allocations a').filter({ hasText: 'Complete the LINQ practice' }).count(), 0);
});

test('planner empty states and unsaved availability are clear', { timeout: 60000 }, async () => {
  await openPlanner();
  await page.getByRole('heading', { name: 'Add your first subject.', exact: true }).waitFor();
  const initialMonday = await page.locator('#minutes-Monday').inputValue();
  await page.locator('#minutes-Monday').fill('135');
  await page.locator('#minutes-Monday').press('Tab');
  await page.locator('.unsaved-hint').waitFor();
  await navigateWithConfirmation('Progress', false);
  assert.ok(page.url().endsWith('/planner'));
  assert.equal(await page.locator('#minutes-Monday').inputValue(), '135');
  await navigateWithConfirmation('Progress', true);
  await page.getByRole('heading', { name: 'No coursework to measure yet.', exact: true }).waitFor();
  await openPlanner();
  assert.equal(await page.locator('#minutes-Monday').inputValue(), initialMonday);
  await addSubject();
  await openPlanner();
  await page.getByRole('heading', { name: 'No tasks to plan yet.', exact: true }).waitFor();
  await newTask('Finish the practice');
  await page.getByRole('button', { name: 'Complete Finish the practice', exact: true }).click();
  await page.getByRole('button', { name: 'Reopen Finish the practice', exact: true }).waitFor();
  await openPlanner();
  await page.getByRole('heading', { name: 'All tasks are complete.', exact: true }).waitFor();
  await page.getByRole('link', { name: 'Progress', exact: true }).click();
  await page.getByText('100%', { exact: true }).first().waitFor();
  assert.equal(await page.locator('[data-stat="open"]').innerText(), '0');
});
