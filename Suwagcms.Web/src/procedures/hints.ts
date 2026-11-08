// Why: the form of a procedure and the form of a step say the same about the same fields, so what they say exists once.
export const PROCEDURE_HINTS = {
    steps: "What the procedure does, top to bottom. Each step goes on to the row below it. An If chooses by a fact, and after it every branch goes on to the row below the If unless it ends in Next: or End.",
    newId: "The procedure's id: how other procedures and articles refer to it. One camelCase word, like orderValuation. To change it later, use Change id on its page.",
    description: "What this procedure is for, in a sentence. The model reads it to choose among the bot's procedures.",
    whenStalled:
        "What the bot does when the conversation gets stuck in this procedure: the instruction it follows. Required when a step collects.",
    whenInterrupted:
        "What happens to this procedure when another one is started on top of it. keep, the default: it waits below and goes on later. discard: it is removed with its local facts.",
    localFacts: "Facts only this procedure sees. They are gone when it ends.",
    localFactName: "How this procedure refers to the fact. One camelCase word, never a global fact's name.",
    stepName:
        "How Next: rows, traces and the bot's points refer to this step. camelCase, unique in the procedure. Renaming it renames every place that points at it; traces then use the new name.",
    collect:
        "The bot asks until these facts are known, and the model fills them from what the customer writes. A fact the model may not write, writableBy assign, is not offered.",
    say: "A fixed text sent to the customer word for word, without the model.",
    assign: "Overwrites these facts with the values you choose here, without asking the customer.",
    start: "Starts this procedure on top of the current one. 'conversation' cannot be started.",
    instruction: "What the model is told to do with the points below, like how to open or what tone to take.",
    points: "Each one is a thing the customer must be told: a name and what must be said. The order is kept, and the bot can check each one was told.",
    knowledge: "What the model reads before it follows this step's instruction, one entry per knowledge base.",
    articles: "Articles of this base the model reads. Name one when you know it holds what is needed.",
    searchQueries:
        "Plain text the bot searches this base with. The CMS stores it and never runs it; name an article when you know which one holds the fact.",
    action: "The action this step runs, without waiting for the customer.",
    actionParameters: "The fact each parameter of the action takes its value from. Only facts that fit its type are offered.",
    actionResults: "The fact each result of the action is written to. A result left as ignored is not kept.",
    onFailure: "What happens when the action fails. No steps: it goes on below.",
    onSuccess: "What happens when the action succeeds. No steps: it goes on below.",
    next: "Goes on at this step instead of the row below, forward or back.",
    conditions: "When every condition here holds, this branch is taken. Branches are tried in order, and the first that holds wins.",
} as const;
