---
description: 'Unit test coverage requirements for production code'
applyTo: '**/*.cs'
---

# Unit Test Coverage

- Maintain at least 80% line coverage of meaningful production code through unit tests.
- Measure the threshold against code changed or added by the work. Also preserve any higher repository-wide coverage baseline.
- Do not count integration, end-to-end, smoke, or manual tests toward the unit test coverage threshold.
- Exclude property getters and setters from the meaningful coverage calculation.
- Exclude constructors that only assign fields or properties.
- Include constructors that perform validation, branching, transformation, resource creation, registration, or any other behavior beyond simple assignment.
- Cover meaningful happy paths, validation, error handling, branches, and edge cases.
- Do not add low-value tests or production-code coverage exclusions merely to increase the reported percentage.
- When the coverage tool includes trivial accessors or assignment-only constructors, interpret the report using these exclusions and document the meaningful coverage calculation in the handoff.
- Before declaring implementation complete, run the repository's unit tests with coverage and report the command, result, and meaningful line coverage percentage.
