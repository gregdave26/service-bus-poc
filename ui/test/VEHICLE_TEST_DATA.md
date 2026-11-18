# Vehicle Test Data Reference

The BDD feature files support vehicle details via **registration number lookup**, which properly updates the form validation state.

## Vehicle Lookup by Registration Number

Use to test the complete vehicle lookup flow with proper form validation.

### Available Demo Regos
- `1ANURAG`
- `1ABC123`
- `1RAC000` (default in tests)
- `1UTE999`

### Feature File Usage
```gherkin
And I have the following vehicle details:
  | rego | 1RAC000 |
When I lookup the vehicle by rego
```

### What It Does
1. Enters the registration number in the rego field
2. Clicks the "Find vehicle" button
3. Waits for the system to look up and return vehicle details
4. Updates the form state with complete vehicle information (`year`, `make`, `model`, `body`, `transmission`, `fuel`)

### Why Lookup Instead of Manual Entry?

The form validation (`canContinue()`) requires:
```javascript
case "yourDetails":
  return hasCompletePersonalDetails(state.personalDetails)
    && (state.vehicle.searchChoice === "Skip"
      || (state.vehicle.searchChoice === "RegoLookup" && state.vehicle.details !== null))
    && state.coverId !== null;
```

**Lookup satisfies validation** because:
- ✅ Sets `searchChoice` to "RegoLookup"
- ✅ Populates `vehicle.details` with complete data from lookup
- ✅ Enables the Continue button

**Manual field entry doesn't work** because:
- ❌ Doesn't set `searchChoice` properly
- ❌ Doesn't populate the `vehicle.details` object
- ❌ Form validation fails, Continue button disabled

---

## How to Modify Vehicle Data

All vehicle data is in the `.feature` file. To test with different vehicles:

### Change the rego value
```gherkin
# Before
And I have the following vehicle details:
  | rego | 1RAC000 |

# After (use a different demo rego)
And I have the following vehicle details:
  | rego | 1ABC123 |
```

### Run tests quickly
```powershell
# No rebuild needed - feature files are read at runtime
.\test-scripts\start-bdd-tests.ps1 -QuickTest
```

---

## Step Definitions Available

### Vehicle Lookup by Rego
```
When I lookup the vehicle by rego
```
- **Required**: `rego` field in vehicle details
- **Finds**: The rego input field (#rego)
- **Clicks**: "Find vehicle" button
- **Waits**: For vehicle details to populate
- **Updates**: Form state with complete vehicle record

### Skip Vehicle (Alternative)
```
Given I skip the vehicle details lookup
```
- Clicks the "Skip" button
- Use when vehicle details aren't needed for the scenario

---

## Testing Scenarios

### Basic Scenario with Rego Lookup
```gherkin
Background:
  Given I am on the digital site join flow
  And I have selected a cover membership
  And I have the following vehicle details:
    | rego | 1RAC000 |

Scenario: Test with vehicle lookup
  When I fill in my personal details
  And I lookup the vehicle by rego
  And I navigate to the Confirm & pay step
  Then I should see the Make Payment button
```

### Testing Different Vehicles
Simply change the rego value in the Background or create scenario-specific overrides:

```gherkin
Scenario: Test with 1UTE999
  Given I have the following vehicle details:
    | rego | 1UTE999 |
  When I fill in my personal details
  And I lookup the vehicle by rego
  And I navigate to the Confirm & pay step
  Then form validation should pass
```

---

## Available Vehicle Models by Rego

| Rego | Year | Make | Model | Body |
|------|------|------|-------|------|
| 1ANURAG | 2021 | Tesla | Model 3 | Sedan |
| 1ABC123 | 2020 | Toyota | Corolla | Sedan |
| 1RAC000 | 2019 | Ford | Ranger | Ute |
| 1UTE999 | 2022 | Holden | Equinox | SUV |

---

## Troubleshooting

### "Continue" button times out after form fill
- ✅ Confirm rego lookup step was called
- ✅ Verify rego value is one of the demo regos
- ✅ Check vehicle details card appears after lookup
- ✅ Check browser console for JavaScript errors

### Vehicle lookup fails
- Verify `#rego` input field is visible
- Verify `Find vehicle` button is clickable
- Check Aspire backend is running (handles lookup)

### Test data not updating
- Use `-QuickTest` flag to skip rebuilds
- Feature file changes take effect immediately
- No npm or dotnet rebuild needed

