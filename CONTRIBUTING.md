# Contributing to TheAppManager

Thank you for your interest in contributing to TheAppManager! This guide will help you get started.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (version specified in `global.json`)
- A code editor such as [Visual Studio](https://visualstudio.microsoft.com/), [Rider](https://www.jetbrains.com/rider/), or [VS Code](https://code.visualstudio.com/) with the C# extension

## Development Setup

1. Fork and clone the repository:
   ```bash
   git clone https://github.com/<your-username>/TheAppManager.git
   cd TheAppManager
   ```
2. Restore dependencies:
   ```bash
   dotnet restore
   ```
3. Build the solution:
   ```bash
   dotnet build
   ```
4. Run the tests to make sure everything works:
   ```bash
   dotnet test
   ```

## Project Structure

```
src/TheAppManager/              - Core library (NuGet package TheAppManager)
src/TheAppManager.Testing/      - Test host and WebApplicationFactory helpers (NuGet package TheAppManager.Testing)
samples/                        - Web, Spectre.Console and Aspire samples, plus their tests
tests/TheAppManager.Tests/      - Unit and integration tests
tests/TheAppManager.DocsSnippets/ - Compiled mirrors of the documentation's C# blocks
tests/TheAppManager.AotSmoke/   - Native AOT smoke app
```

## Making Changes

`dev` is the default branch: branch from it and open pull requests against it.

1. Create a feature branch from `dev`:
   ```bash
   git checkout -b feature/your-feature-name
   ```
2. Make your changes in small, focused commits.
3. Write or update tests to cover your changes.
4. Ensure all tests pass:
   ```bash
   dotnet test
   ```

## Commit Messages

The project uses [Conventional Commits](https://www.conventionalcommits.org/), because
[release-please](https://github.com/googleapis/release-please) reads them to pick the next version and write the changelog.

- Format: `type(scope): summary`, e.g. `fix: keep the module order when replacing`.
- `feat`, `fix`, `perf` and `refactor` appear in the changelog; `docs`, `test`, `build`, `ci` and `chore` do not.
- A breaking change adds `!` after the type (`feat!: ...`) and a `BREAKING CHANGE: <what breaks and how to migrate>` footer.
- Pull requests are squash-merged, so the PR title becomes the commit on `dev`. Release-please then opens the release PR.

## Documentation Snippets

Every C# block in `README.md`, `docs/migration-v2-to-v3.md` and the packages' `NUGET.md` files must have a mirrored
snippet. Put `<!-- snippet: name -->` on the line before the block, and the same code between
`// begin-snippet: name` and `// end-snippet` in `tests/TheAppManager.DocsSnippets/Snippets/*.cs`. The build compiles
the snippet, and `DocSnippetsTests` fails when the block and the snippet drift apart.

## Code Style

This project uses an `.editorconfig` to enforce consistent formatting. Please ensure your editor respects it, or run:

```bash
dotnet format
```

General guidelines:

- Enable nullable reference types (`<Nullable>enable</Nullable>` is set project-wide).
- Use `latest` C# language features where appropriate.
- Follow standard .NET naming conventions (PascalCase for public members, camelCase for locals).

## Pull Request Process

1. Update documentation (README, XML doc comments) if your changes affect public APIs.
2. Ensure the solution builds without warnings:
   ```bash
   dotnet build --configuration Release
   ```
3. Ensure all tests pass.
4. Push your branch and open a Pull Request against `dev`. It is squash-merged.
5. Title the PR as a [Conventional Commit](https://www.conventionalcommits.org/): `type(scope): summary`, with `!` before the colon for a breaking change (`feat!: ...`). PRs are squash-merged, so the title becomes the commit release-please reads to pick the next version; the `PR title` check enforces it. Allowed types: `feat`, `fix`, `perf`, `refactor`, `revert`, `docs`, `test`, `build`, `ci`, `chore`.
6. Describe what your PR does and why.
7. Address any review feedback.

## Reporting Issues

- Use the [GitHub issue tracker](https://github.com/Atypical-Consulting/TheAppManager/issues).
- Search existing issues before creating a new one.
- Provide a clear description, steps to reproduce, and the expected vs. actual behavior.

## Code of Conduct

Please be respectful and constructive in all interactions. We are committed to providing a welcoming and inclusive experience for everyone.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
