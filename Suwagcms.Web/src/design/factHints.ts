export const CONSTRAINT_HINTS = {
    values: "The only values allowed, separated by commas; at least two. Use this when the answer comes from a fixed list. 'other' cannot be one of them: a reply writes it for anything outside the list.",
    min: "The smallest whole number allowed. A smaller value is refused.",
    max: "The largest whole number allowed. A larger value is refused.",
    maxLength: "The longest text allowed, in characters; above 0. A longer value is refused.",
} as const;

export const PATTERN_HINTS = {
    regex: "Optional. A regular expression the whole value must match, for a form like a postal code or a phone number. Left empty, any text up to the maximum length fits.",
    means: "The form in plain words, like 'a Polish postal code'. Required with a regular expression, since code cannot say what one means.",
    example: "Optional. A value that fits, like 00-950. It must match the regular expression.",
    tellCustomer:
        "When the customer hears about the form. upfront, the default: before they answer. afterRefusal: only once a value has been refused.",
} as const;

export const TYPE_HINT =
    "What kind of value it holds: text, enum (one value from a list) or int (a whole number). The type decides which limits you can set.";

export const FACT_HINTS = {
    type: TYPE_HINT,
    description: "The model reads this to understand a message and to know what to fill in. Say plainly what the value is.",
    writableBy:
        "Who may write the fact; each level includes the one before. assign: only the procedure, through assign steps and action results, so a collect step cannot use it. set, the default: also the model, once, while the fact is empty. correct: also the model, when the customer corrects a known value.",
} as const;
