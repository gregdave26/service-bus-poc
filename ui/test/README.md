# Playwright BDD Testing with Gherkin

This directory contains both traditional Playwright tests and BDD-style tests using Gherkin and Cucumber.

## Prerequisites

Before running tests, ensure:

1. **Aspire and Services are Running**
   ```bash
   # In the project root directory
   .\scripts\run-aspire.ps1 -NoBrowser
   ```
   - This starts the Service Bus emulator and backend services
   - Dashboard will be available at http://localhost:5100
   - Allow 30-60 seconds for services to fully initialize

2. **UI Server is Running**
   ```bash
   cd ui
   npm run dev
   # or
   npm start
   ```
   - Makes the UI available at http://localhost:5173 (dev) or http://localhost:5100 (production)

3. **Dependencies Installed**
   ```bash
   cd ui
   npm install
   ```

## Quick Start

Run the one-liner startup script (PowerShell 7+):
```powershell
.\scripts\start_tests.ps1
```

This will start Aspire, UI server, and run BDD tests in parallel.

## Structure

```
test/
├── features/                           # Gherkin feature files
│   ├── adyen-payment-button.feature    # @wip - blocked on server checkout endpoints
│   ├── card-validity-and-terms-gating.feature
│   └── make-payment-button.feature     # @wip - blocked on server checkout endpoints
├── steps/                              # Step definitions for Cucumber
│   ├── adyen-payment-button.steps.js
│   ├── card-validity-and-terms-gating.steps.js
│   └── make-payment-button.steps.js
└── card-validity-and-terms-gating.test.js  # Traditional Playwright tests (JavaScript)
```

## Running Tests

### Traditional Playwright Tests (JavaScript)
These run with Playwright's built-in test runner:

```bash
# Run all Playwright tests
npx playwright test

# Run specific test file
npx playwright test card-validity-and-terms-gating.test.js

# Run with reporter
npx playwright test --reporter=list
```

### BDD Tests (Gherkin + Cucumber)
To run BDD tests, use the following command (requires Aspire running):

```bash
# Run BDD tests with explicit step definitions and extended timeout
# This is the recommended command for running BDD tests locally
npx cucumber-js --import 'test/steps/card-validity-and-terms-gating.steps.js' test/features/card-validity-and-terms-gating.feature

# Run specific scenario
npx cucumber-js --import 'test/steps/card-validity-and-terms-gating.steps.js' --name "Make Payment button" test/features/card-validity-and-terms-gating.feature

# Run with dry-run (check steps without executing)
npx cucumber-js --import 'test/steps/card-validity-and-terms-gating.steps.js' --dry-run test/features/card-validity-and-terms-gating.feature
```

**Note**: The Cucumber configuration file (`cucumber.js`) includes a 30-second timeout for steps to handle form navigation complexity. When running via `npm run test:bdd`, ensure you're running from the `ui` directory with Aspire services running.

**`@wip` scenarios**: Scenarios tagged `@wip` (the Make Payment and Adyen features) are excluded from the default run. They depend on server endpoints that are not implemented yet (cart, members, checkout, and an Adyen gateway config), so they fail at the Continue step until those exist. Run them explicitly with:

```powershell
$env:BDD_TAGS='@wip'; npm run test:bdd
```

Once the endpoints exist, remove the `@wip` tags from the feature files. `BDD_TAGS` overrides the default tag filter in `cucumber.cjs`; cucumber-js ANDs `--tags` with the profile filter, so `--tags` alone can't select `@wip`.

## Current Test Status

### Passing Tests (3/5 scenarios)
✅ Verify the payment form structure  
✅ Terms checkbox state persists through form interactions  
✅ Button gating with Adyen card component

### In Progress (2/5 scenarios)
⏳ Make Payment button is disabled until card valid and terms accepted  
⏳ Payment flow blocked without terms acceptance  
*(These test complex form navigation requiring all 8+ fields to be validated)*

## Test Coverage

### Card Validity and Terms Gating
Both traditional and BDD tests cover:

1. **Make Payment Button Gating**
   - Verifies button is properly disabled until conditions are met
   - Button should enable when: card is valid AND terms are accepted
   - Payment submission is gated by both conditions

2. **Terms Acceptance Flow**
   - User can check/uncheck Roadside Assistance terms
   - Checkbox state persists through form interactions
   - Button state updates when terms change

3. **Adyen Integration**
   - Card validity tracked via `onValidity` callback
   - Terms acceptance propagated via `termsAccepted` prop
   - Payment submission controlled via ref-based method

4. **Payment Flow Blocking**
   - No payment possible without terms acceptance
   - Button remains disabled if either condition fails
   - Proper state transitions and error handling

## Writing New BDD Tests

### 1. Create a Feature File (`.feature`)
```gherkin
Feature: Your feature description
  Scenario: What should happen
    Given precondition
    When action happens
    Then expected result
```

### 2. Create Step Definitions (`.steps.js`)
```javascript
import { Given, When, Then } from '@cucumber/cucumber';

Given('precondition', async function() {
  // Implementation
});

When('action happens', async function() {
  // Implementation
});

Then('expected result', async function() {
  // Implementation
});
```

### 3. Run Tests
```bash
npx cucumber-js test/features/your-feature.feature
```

## Key Features

### Context Sharing
Step definitions use `this` context to share data between steps:
```javascript
this.makePaymentBtn = btn;  // Set in one step
this.makePaymentBtn.isDisabled();  // Use in another
```

### Browser Lifecycle
Cucumber handles browser setup/teardown:
- `Before`: Launches browser before each scenario
- `After`: Closes browser after each scenario

### Reporting
- HTML reports generated at `test-results/cucumber-report.html`
- Console progress bar output
- Detailed step logging

## Debugging

### Enable Chrome DevTools
Modify step definitions to add delays or open DevTools:
```javascript
// Pause execution
await page.pause();

// Open DevTools
await page.context().tracing.start({ screenshots: true });
```

### View Logs
Tests log detailed information to console:
```
✓ Cover membership selected
✓ Personal details filled
✓ Found Make Payment button: "Make Payment >"
✓ Terms checkbox is: CHECKED
```

## CI/CD Integration

### GitHub Actions Example
```yaml
- name: Run Playwright tests
  run: npm test

- name: Run Gherkin tests
  run: npx cucumber-js
```

## Notes

- Traditional Playwright tests (`*.test.js`) use Playwright's test runner
- BDD tests (`*.feature` + `*.steps.js`) use Cucumber framework
- Both approaches test the same functionality with different philosophies
- Choose tests based on audience: developers vs. business stakeholders

## Resources

- [Playwright Documentation](https://playwright.dev/)
- [Cucumber.js Documentation](https://cucumber.io/docs/cucumber/)
- [Gherkin Syntax](https://cucumber.io/docs/gherkin/)
