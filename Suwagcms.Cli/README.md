# Suwagcms.Cli

The `suwagcms` command: read and change what a bot knows and does in [Suwagcms](https://github.com/Manomenu/suwagcms), the CMS of a
bot's facts, knowledge bases, articles, procedures, actions and persona.

```sh
dotnet tool install -g Suwagcms.Cli
suwagcms server http://127.0.0.1:8220   # where your Suwagcms runs
suwagcms status
suwagcms --help
```

Needs the .NET 10 runtime. Update with `dotnet tool update -g Suwagcms.Cli`. Every command, the file of changes and the exit codes are
in the [README of the repository](https://github.com/Manomenu/suwagcms#readme).
