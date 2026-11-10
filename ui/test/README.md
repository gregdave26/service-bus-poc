# Playwright BDD Testing with Gherkin

This directory contains both traditional Playwright tests and BDD-style tests using Gherkin and Cucumber.

## Structure

```
test/
├── features/                           # Gherkin feature files
│   └── card-validity-and-terms-gating.feature
├── steps/                              # Step definitions for Cucumber
│   └── card-validity-and-terms-gating.steps.js
├── card-validity-and-terms-gating.test.js  # Traditional Playwright tests (JavaScript)
└── make-payment-button.test.js        # Existing Playwright tests
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
To run BDD tests, you need to install Cucumber first:

```bash
# Install Cucumber and required dependencies
npm install --save-dev @cucumber/cucumber

# Run Gherkin features
npx cucumber-js

# Run specific feature
npx cucumber-js test/features/card-validity-and-terms-gating.feature

# Run with specific format
npx cucumber-js --format progress-bar
```

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
