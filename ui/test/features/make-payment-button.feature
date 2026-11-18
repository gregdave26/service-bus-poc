@wip
Feature: Make Payment Button Enable and Disable Behaviour
  As a customer joining RAC
  I want the Make Payment button to reflect my card and terms state
  So that I can only attempt payment when everything required is in place

  Background:
    Given I am on the digital site join flow
    And I have selected a cover membership

  Scenario: Make Payment button stays disabled after accepting terms without a valid card
    When I fill in my personal details
    And I navigate to the Confirm & pay step
    Then I should see the Make Payment button
    When I accept the Roadside Assistance terms
    Then the Make Payment button should remain disabled

  Scenario: A Continue or Make Payment action button is available after joining
    Then a Continue or Make Payment button should be visible

  Scenario: Digital site page renders its title and headings
    Then the page title should contain "Contact events"
    And at least one heading should be visible
