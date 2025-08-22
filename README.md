About RockBlock9603

This project is a C# library, CLI, and test suite for the Iridium RockBLOCK 9603 satellite modem. It implements AT command handling over serial communication, supporting both text and binary SBD messaging, device queries, and unsolicited event responses.

I built this as a portfolio project to demonstrate my engineering approach:

Protocol Handling → Implements Iridium’s AT command set with structured request/response parsing.

Lifecycle & Error Management → Async I/O, cancellation, safe disposal, and fault handling for serial communication.

Testing Discipline → Includes NUnit integration tests and hardware-backed verification.

Tooling → Provides a JSON-based CLI for easy automation and scripting.

While not intended for production deployment, the project is production-adjacent: it demonstrates the design, testing, and tooling practices I bring to professional software engineering.
