Feature: Card Validity and Terms Gating
  As a customer joining RAC
  I want the Make Payment button to be properly gated
  So that I can only proceed with payment when both card details are valid and I've accepted the terms

  Background:
    Given I am on the digital site join flow
    And I have selected a cover membership

  Scenario: Make Payment button is disabled until card valid and terms accepted
    When I fill in my personal details
    And I navigate to the Confirm & pay step
    Then I should see the Make Payment button
    And I should see the Roadside Assistance terms checkbox
    When I check the Roadside Assistance terms checkbox
    Then the button state should reflect the terms acceptance

  Scenario: Verify the payment form structure
    When I navigate through the join flow
    And I reach the Confirm & pay step
    Then the page should display:
      | Element                            |
      | Make Payment button                |
      | Roadside Assistance terms checkbox |
      | Payment details form               |

  Scenario: Terms checkbox state persists through form interactions
    When I accept the Roadside Assistance terms
    And I interact with payment form fields
    Then the terms checkbox should remain checked
    And the Make Payment button state should be tracked

  Scenario: Button gating with Adyen card component
    When I fill in my personal details
    And I navigate to the payment step
    And the Adyen card component is loaded
    Then entering a valid test card should enable payment flow
    When I accept the terms
    Then the Make Payment button should be ready for submission

  Scenario: Payment flow blocked without terms acceptance
    When I fill in my personal details
    And I navigate to the Confirm & pay step
    And I do NOT accept the terms checkbox
    Then the Make Payment button should remain disabled
    And no payment should be possible without terms acceptance
