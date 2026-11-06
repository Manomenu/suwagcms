// Why: the panel of a procedure and the form of a step say the same about the same fields, so what they say exists once.
export const PROCEDURE_HINTS = {
    blocks: "A step holds one block. listen waits for the next message. collect asks until the listed facts are known. say sends a fixed text. assign overwrites facts with values you choose. start starts another procedure. inform has the model tell the customer named points. action runs an action.",
    newId: "The procedure's id: how other procedures and articles refer to it. One camelCase word, like orderValuation. To change it later, use Change id in the creator.",
    description: "What this procedure is for, in a sentence. The model reads it to choose among the bot's procedures.",
    whenStalled:
        "What the bot does when the conversation gets stuck in this procedure: the instruction it follows. Required when a step collects.",
    whenInterrupted:
        "What happens to this procedure when another one is started on top of it. keep, the default: it waits below and goes on later. discard: it is removed with its local facts.",
    localFacts: "Facts only this procedure sees. They are gone when it ends.",
    localFactName: "How this procedure refers to the fact. One camelCase word, never a global fact's name.",
    stepName:
        "How next and the first step refer to this step. camelCase, unique in the procedure. Renaming it renames every place that points at it.",
    firstStep: "The procedure starts here. One step at a time carries the mark.",
    collect:
        "The bot asks until these facts are known, and the model fills them from what the customer writes. A fact the model may not write, writableBy assign, is not offered.",
    say: "A fixed text sent to the customer word for word, without the model.",
    assign: "Overwrites these facts with the values you choose here, without asking the customer.",
    start: "Starts this procedure on top of the current one. 'conversation' cannot be started.",
    instruction: "What the model is told to do with the points below, like how to open or what tone to take.",
    points: "Each one is a thing the customer must be told: a name and what must be said. The order is kept, and the bot can check each one was told.",
    articles: "Articles the model reads before it follows the instruction. Name one when you know it holds what is needed.",
    searchQueries:
        "Plain text the bot searches with. The CMS stores it and never runs it; name an article when you know which one holds the fact.",
    action: "The action this step runs, without waiting for the customer.",
    actionParameters: "The fact each parameter of the action takes its value from. Only facts that fit its type are offered.",
    actionResults: "The fact each result of the action is written to. A result left as ignored is not kept.",
    onFailure: "The step to go to when the action fails. Not connected: the procedure has nowhere to go from a failure.",
    goTo: "The step that comes next. Not connected: the procedure ends after this step.",
    ruleGoTo: "The step to go to when every condition of this rule holds. Rules are tried in order, and the first that holds is taken.",
    otherwiseGoTo: "The step to go to when no rule above holds.",
} as const;
