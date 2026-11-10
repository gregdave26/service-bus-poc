# VS Code Cucumber Extension Setup

## Configuration Complete ✅

The VS Code Cucumber extension has been configured to find and link step definitions to feature files.

## Required Extension

Make sure you have installed one of these Cucumber extensions:
- **Recommended**: "Cucumber (Gherkin) Full Support" by Alexander Krechik (alexkrechik.cucumberautocomplete)
- Alternative: "Gherkin (Cucumber) Full Support" by Oskar Nordling

## What's Configured

The `.vscode/settings.json` file includes:

### Step Definition Discovery
```json
"cucumberautocomplete.steps": [
  "ui/test/steps/**/*.steps.js",
  "test/steps/**/*.steps.js"
]
```
This tells the extension where to find step definitions (both in `ui/` and root-level `test/`)

### Feature File Recognition
```json
"cucumberautocomplete.features": [
  "ui/test/features/**/*.feature",
  "test/features/**/*.feature"
]
```
This tells the extension where to find feature files

### Gherkin File Association
```json
"files.associations": {
  "*.feature": "gherkin"
}
```
Ensures `.feature` files are recognized as Gherkin syntax

## How to Use

### Navigate to Step Definition
1. Open a feature file (e.g., `ui/test/features/card-validity-and-terms-gating.feature`)
2. Click on any step text (e.g., "I am on the digital site join flow")
3. Use **Ctrl+Click** (or Cmd+Click on Mac) to jump to the step definition
4. Or right-click → "Go to Definition"

### Get Step Autocomplete
1. In a feature file, start typing a step (e.g., "Given I...")
2. The extension should provide autocomplete suggestions from your step definitions
3. Steps will be highlighted when they're recognized

### Preview Step Definition
1. Hover over a step in a feature file
2. A tooltip should show the matching step definition
3. Gherkin syntax will be properly highlighted

## Troubleshooting

### Steps aren't linking or autocompleting?

1. **Reload VS Code**
   - Press `Ctrl+Shift+P` (or `Cmd+Shift+P`)
   - Type "Reload Window"
   - Press Enter
   - This forces the extension to re-scan files

2. **Check Extension is Installed**
   - Open VS Code Extensions (Ctrl+Shift+X)
   - Search for "Cucumber" or "Gherkin"
   - Verify one of the recommended extensions is installed and enabled

3. **Verify File Paths**
   - Feature files: `ui/test/features/*.feature`
   - Step definitions: `ui/test/steps/*steps.js`
   - Check that files exist in these locations

4. **Check for Exact Step Matches**
   - Step text in feature file must EXACTLY match the string in `Given()`, `When()`, `Then()`
   - Example: Feature has "When I fill in my personal details"
   - Step definition must have: `When('I fill in my personal details', ...)`
   - Case-sensitive!

5. **Enable Debug Logging**
   - Open Output panel (View → Output)
   - Select "Cucumber AutoComplete" from dropdown
   - This shows which files the extension is scanning

6. **Check VS Code Settings**
   - Open Settings (Ctrl+,)
   - Search for "cucumber"
   - Verify the paths look correct and match your directory structure

7. **Try a Specific Example**
   - In `ui/test/features/card-validity-and-terms-gating.feature`
   - Open line 7: "Given I am on the digital site join flow"
   - Ctrl+Click on this step
   - Should jump to line 21 in `ui/test/steps/card-validity-and-terms-gating.steps.js`

### The extension shows "Undefined" for all steps?

This means the step definitions aren't being found. Try:

1. Ensure the `.vscode/settings.json` file exists in the workspace root
2. Check that the paths in `cucumberautocomplete.steps` are correct
3. Reload VS Code
4. Check the extension's output panel for errors

### Some steps work but others don't?

Check for:
- Extra spaces or different wording in feature vs. step definition
- Case sensitivity differences
- Special characters or punctuation differences
- Regular expressions in step definitions (some extensions struggle with these)

## Step Definition Patterns

All step definitions use a simple pattern matching approach:

```javascript
// Basic steps (these work best)
Given('I am on the digital site join flow', async function() { ... });

// Steps with parameters
Then('the page should display:', async function(dataTable) { ... });

// Steps with variable text (regex-based)
// These might not link - consider simplifying
```

## Current Step Definitions

The following steps are currently defined and should be linkable:

**Background Steps (Given):**
- I am on the digital site join flow
- I have selected a cover membership

**When Steps:**
- I fill in my personal details
- I navigate to the Confirm & pay step
- I navigate through the join flow
- I reach the Confirm & pay step
- I check the Roadside Assistance terms checkbox
- I accept the Roadside Assistance terms
- I do NOT accept the terms checkbox
- I interact with payment form fields
- I navigate to the payment step
- the Adyen card component is loaded
- entering a valid test card should enable payment flow
- I accept the terms

**Then Steps:**
- I should see the Make Payment button
- I should see the Roadside Assistance terms checkbox
- the button state should reflect the terms acceptance
- the page should display:
- the terms checkbox should remain checked
- the Make Payment button state should be tracked
- the Make Payment button should be ready for submission
- the Make Payment button should remain disabled
- no payment should be possible without terms acceptance

## Next Steps

1. Reload VS Code: `Ctrl+Shift+P` → "Reload Window"
2. Open a feature file
3. Try Ctrl+Click on a step to navigate to its definition
4. Report any issues in the project documentation

## Extension References

- [alexkrechik.cucumberautocomplete](https://marketplace.visualstudio.com/items?itemName=alexkrechik.cucumberautocomplete)
- [Oskar Nordling's Gherkin](https://marketplace.visualstudio.com/items?itemName=oscarnordling.vscode-gherkin)
