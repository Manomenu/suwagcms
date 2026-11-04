# Suwagcms.Sdk

The SDK of a .NET bot (.NET 9 or later) built on Suwagcms.

```sh
dotnet add package Suwagcms.Sdk
```

It holds the definition format (`Suwagcms.Sdk.Format`, JSON schema in `definition-format.schema.json`) and the
validator (`Suwagcms.Sdk.Validation`):

```csharp
IReadOnlyList<Mistake> mistakes = DefinitionValidator.ValidateJson(json, ValidationStage.Version);
```

It returns every mistake at once, each with a stable code and a fix; an empty list means the definition passes.
It needs no database and no network.
