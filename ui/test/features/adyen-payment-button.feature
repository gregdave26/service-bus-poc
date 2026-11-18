@wip
Feature: Adyen Payment Component Pay Button
  As a customer joining RAC paying annually by card
  I want the Adyen card component to load without its own pay button
  So that payment is only submitted through the Make Payment button

  Background:
    Given I am on the digital site join flow
    And I have selected a cover membership

  Scenario: Adyen pay button is hidden when the component renders
    When I fill in my personal details
    And I navigate to the Confirm & pay step
    And I choose to pay annually by card
    Then the Adyen card component should be displayed
    And the Adyen pay button should be hidden

  Scenario: Adyen payment form container is attached for card payment
    When I fill in my personal details
    And I navigate to the Confirm & pay step
    And I choose to pay annually by card
    Then the Adyen payment form container should be attached
