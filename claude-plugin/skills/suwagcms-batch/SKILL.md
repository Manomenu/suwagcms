---
name: suwagcms-batch
description: Make many Suwagcms changes that take effect together, or none, with suwagcms apply and a file of changes. Use when an agent writes several articles, facts, knowledge bases, actions or procedures at once, or when changes only work together, such as removing a link and the article it points at.
---

# Batch changes in Suwagcms

`suwagcms apply` takes a file of changes and applies all of them or none. The rules judge the result of the whole file, so
the order inside it means nothing, and every mistake comes back at once. It is an ordinary save with more than one change in
it: read the main `suwagcms` skill for the server, the bot, `--json` and refusals.

## Steps

1. Write the file, then try it: `suwagcms apply changes.yaml --dry-run --json`. It answers as the real command would and changes nothing.
2. Exit code `1`: fix every mistake in `mistakes`, each as its `fix` says (`change` is the change's position in the file, from 1), and try again.
3. Apply it with the same file: `suwagcms apply changes.yaml --json`. `notReady` lists what still stops a version, which never refuses a file.

## The file

```yaml
bot: rzeczoznawca            # optional; refused if it is not the bot in use
changes:
  - add: article             # add, edit or delete; kinds: fact, knowledgeBase, article, action, procedure, persona
    id: rabaty
    knowledgeBase: oferta
    title: Rabaty
    description: Discounts on a valuation.
    llmGuide: Ask about the kind of property first.
    content: Zobacz [Cennik](cms://article/cennik).
  - add: knowledgeBase
    id: oferta
    title: Oferta
    language: pl
    embedding: default       # the bot's embedding profile; results: how many one search gives, 1 to 20
    results: 5
  - edit: fact
    name: customer.city      # a fact has a name, the others an id
    ifRevision: 7            # optional: refuse the whole file if it is no longer this revision
    description: The city of the property.
  - delete: article
    id: stary-cennik
```

- A change's other fields are those of the resource's own save. An edit changes only the fields it gives.
- `edit: persona` is the exception: it takes no `id` and replaces the whole persona, as `persona edit --file` does, so what it leaves out becomes empty. A persona is never added or deleted.
- A file lists changes; it is not a copy of the bot. A resource the file does not name stays as it is, and nothing is deleted for being absent.
- One resource may be named once: two changes to it are refused with `change.twice`, since what they did together would depend on their order.
- Changes that only work together belong in one file: an article and the links to it can go at once, and articles may come before their knowledge base. Two articles linking to each other are still a loop, refused with `reference.loop`.
- `apply` never creates a version or pushes; those are separate steps.
