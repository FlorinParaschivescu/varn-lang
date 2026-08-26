## Summary

Describe the observable behavior and why it belongs in the current roadmap milestone.

## Contract changes

- Syntax / structural representation:
- Types and static validation:
- Effects and capabilities:
- Runtime and resource accounting:
- Diagnostics:

## Verification

- [ ] `dotnet build Varn.slnx --configuration Release`
- [ ] `dotnet run --project tests/Varn.Tests --configuration Release --no-build`
- [ ] `dotnet format Varn.slnx --verify-no-changes --no-restore`
- [ ] Relevant CLI and module smoke tests pass
- [ ] Specifications and examples are updated

## Security and determinism

Explain any new host access, nondeterminism, or trusted-code boundary. Write `None` if the change introduces none.
