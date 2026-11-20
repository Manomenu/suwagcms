import { Button, Input, Select } from "@maniumek/suwfrontkit";
import type { Fact } from "./api";
import { conditionKinds, freshCondition, kindOf, valuesOffered, type Condition, type ConditionKind } from "./draft";

const KIND_LABEL: Record<ConditionKind, string> = { oneOf: "is one of", range: "is between", known: "is known" };

function ConditionValue({
    fact,
    condition,
    onChange,
}: {
    fact: Fact | undefined;
    condition: Condition;
    onChange: (condition: Condition) => void;
}) {
    const kind = kindOf(condition);
    if (kind === "known") {
        return (
            <Select
                aria-label="Known or unknown"
                value={condition.known === false ? "no" : "yes"}
                onChange={(event) => {
                    onChange({ known: event.target.value === "yes" });
                }}
            >
                <option value="yes">known</option>
                <option value="no">not known yet</option>
            </Select>
        );
    }
    if (kind === "range") {
        const range = condition.range ?? {};
        const number = (text: string) => (text === "" ? undefined : Number(text));
        return (
            <span className="flex items-center gap-1 text-sm">
                <Input
                    aria-label="From"
                    type="number"
                    className="w-20"
                    value={range.min ?? ""}
                    onChange={(event) => {
                        const min = number(event.target.value);
                        onChange({
                            range: { ...(min === undefined ? {} : { min }), ...(range.max === undefined ? {} : { max: range.max }) },
                        });
                    }}
                />
                to
                <Input
                    aria-label="To"
                    type="number"
                    className="w-20"
                    value={range.max ?? ""}
                    onChange={(event) => {
                        const max = number(event.target.value);
                        onChange({
                            range: { ...(range.min === undefined ? {} : { min: range.min }), ...(max === undefined ? {} : { max }) },
                        });
                    }}
                />
            </span>
        );
    }
    const chosen = condition.oneOf ?? [];
    const values = valuesOffered(fact);
    if (values !== null) {
        return (
            <span className="flex flex-wrap gap-x-3 gap-y-1 text-sm">
                {values.map((value) => (
                    <label key={value} className="flex items-center gap-1">
                        <input
                            type="checkbox"
                            className="accent-primary"
                            checked={chosen.includes(value)}
                            onChange={(event) => {
                                onChange({ oneOf: event.target.checked ? [...chosen, value] : chosen.filter((other) => other !== value) });
                            }}
                        />
                        {value}
                    </label>
                ))}
            </span>
        );
    }
    return (
        <Input
            aria-label="Values, separated by commas"
            value={chosen.join(", ")}
            onChange={(event) => {
                onChange({
                    oneOf: event.target.value
                        .split(",")
                        .map((value) => value.trim())
                        .filter((value) => value !== ""),
                });
            }}
        />
    );
}

function ConditionRow({
    fact,
    condition,
    facts,
    onChange,
    onRemove,
    onFact,
}: {
    fact: string;
    condition: Condition;
    facts: Fact[];
    onChange: (condition: Condition) => void;
    onRemove: () => void;
    onFact: (name: string) => void;
}) {
    const known = facts.find((candidate) => candidate.name === fact);
    return (
        <div className="flex flex-col gap-1 rounded-md bg-secondary p-2">
            <div className="flex items-center gap-1">
                <Select
                    aria-label="Fact"
                    className="flex-1"
                    value={fact}
                    onChange={(event) => {
                        onFact(event.target.value);
                    }}
                >
                    {known === undefined && <option value={fact}>{fact}</option>}
                    {facts.map((candidate) => (
                        <option key={candidate.name} value={candidate.name}>
                            {candidate.name}
                        </option>
                    ))}
                </Select>
                <Select
                    aria-label="Condition"
                    className="w-32 shrink-0"
                    value={kindOf(condition)}
                    onChange={(event) => {
                        onChange(freshCondition(event.target.value as ConditionKind));
                    }}
                >
                    {conditionKinds(known).map((kind) => (
                        <option key={kind} value={kind}>
                            {KIND_LABEL[kind]}
                        </option>
                    ))}
                </Select>
                <Button type="button" variant="ghost" size="icon" aria-label={`Remove the condition on ${fact}`} onClick={onRemove}>
                    ✕
                </Button>
            </div>
            <ConditionValue fact={known} condition={condition} onChange={onChange} />
        </div>
    );
}

export function ConditionsEditor({
    when,
    facts,
    onChange,
}: {
    when: Record<string, Condition>;
    facts: Fact[];
    onChange: (when: Record<string, Condition>) => void;
}) {
    const conditions = Object.entries(when);
    const unused = facts.filter((fact) => !(fact.name in when));
    return (
        <div className="flex flex-col gap-2">
            {conditions.map(([fact, condition]) => (
                <ConditionRow
                    key={fact}
                    fact={fact}
                    condition={condition}
                    facts={facts}
                    onChange={(changed) => {
                        onChange({ ...when, [fact]: changed });
                    }}
                    onRemove={() => {
                        onChange(Object.fromEntries(conditions.filter(([other]) => other !== fact)));
                    }}
                    onFact={(name) => {
                        onChange(
                            Object.fromEntries(
                                conditions.map(([other, value]) => (other === fact ? [name, freshCondition("oneOf")] : [other, value])),
                            ),
                        );
                    }}
                />
            ))}
            {unused[0] !== undefined && (
                <div>
                    <Button
                        type="button"
                        variant="outline"
                        size="xs"
                        onClick={() => {
                            onChange({ ...when, [unused[0]?.name ?? ""]: freshCondition("oneOf") });
                        }}
                    >
                        {conditions.length === 0 ? "+ Choose a fact" : "+ and"}
                    </Button>
                </div>
            )}
            {facts.length === 0 && <p className="text-xs text-muted-foreground">This bot has no facts to compare.</p>}
        </div>
    );
}

export function conditionsText(when: Record<string, Condition>): string {
    return Object.entries(when)
        .map(([fact, condition]) => {
            if (condition.range) {
                const { min, max } = condition.range;
                if (min !== undefined && max !== undefined) return `${fact} is between ${min} and ${max}`;
                if (min !== undefined) return `${fact} is at least ${min}`;
                return max === undefined ? `${fact} has a range not set` : `${fact} is at most ${max}`;
            }
            if (condition.known !== undefined) return `${fact} is ${condition.known ? "known" : "not known yet"}`;
            const values = condition.oneOf ?? [];
            return values.length === 0 ? `${fact} has no value chosen yet` : `${fact} is one of ${values.join(", ")}`;
        })
        .join(" and ");
}
