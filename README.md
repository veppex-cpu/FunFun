# Conway's Game of Life API Submission

This repository contains a C# implementation of a Conway's Game of Life HTTP API targeting `net10.0`.

Main project:

- [csharp-app](csharp-app)

Test project:

- [csharp-app.Tests](csharp-app.Tests)

Primary documentation:

- [API README](csharp-app/README.md)
- [Test README](csharp-app.Tests/README.md)
- [Architecture diagram](csharp-app/docs/architecture.svg)
- [Test coverage diagram](csharp-app.Tests/docs/test-coverage.svg)
- [Big board animation example](csharp-app/docs/big-board-animation.html)

Run the API:

```sh
dotnet run --project csharp-app --urls http://127.0.0.1:5010
```

Run the tests:

```sh
dotnet run --project csharp-app.Tests
```

Current verification status:

- API build succeeds with `0` errors.
- Test suite has `63` passing tests.
- Tests include selected real HTTP integration checks that start the API on temporary localhost ports.

