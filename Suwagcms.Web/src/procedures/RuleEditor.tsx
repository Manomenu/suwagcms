import { Button } from "@/design/button";
import { Input } from "@/design/input";
import { Select } from "@/design/select";

import type { Fact } from "./api";
import {
    canRemoveRule,
    conditionKinds,
    freshCondition,
    kindOf,
    removeRule,
    setConditions,
    setRuleTarget,
    valuesOffered,
    type Condition,
    type ConditionKind,
    type Draft,
    type Rule,
} from "./draft";

const KIND_LABEL: Record<ConditionKind, string> = { oneOf: "is one of", range: "is between", known: "is known" };

interface Context {
    draft: Draft;
    step: string;
    facts: Fact[];
    edit: (change: (draft: Draft) => Draft) => void;
}

function TargetSelect({ label, value, draft, onChange }: { label: string; value: string; draft: Draft; onChange: (to: string) => void }) {
    return (
        <label className="flex items-center gap-2 text-sm">
            <span className="shrink-0">{label}</span>
            <Select
                aria-label={label}
                className="flex-1"
                value={value in draft.steps ? value : ""}
                onChange={(event) => {
                    onChange(event.target.value);
                }}
            >
                <option value="">(not connected)</option>
                {Object.keys(draft.steps).map((name) => (
                    <option key={name} value={name}>
                        {name}
                    </option>
                ))}
            </Select>
        </label>
    );
}

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
                <Button type="button" variant="ghost" size="xs" aria-label={`Remove the condition on ${fact}`} onClick={onRemove}>
                    ✕
                </Button>
            </div>
            <ConditionValue fact={known} condition={condition} onChange={onChange} />
        </div>
    );
}

function RuleCard({ rule, index, context }: { rule: Rule; index: number; context: Context }) {
    const { draft, step, facts, edit } = context;
    const stepValue = draft.steps[step];
    const isDefault = rule.when === undefined;
    const conditions = Object.entries(rule.when ?? {});
    const setWhen = (when: Record<string, Condition>) => {
        edit((current) => setConditions(current, step, index, when));
    };
    const unused = facts.filter((fact) => !(fact.name in (rule.when ?? {})));
    return (
        <li className="flex flex-col gap-2 rounded-lg border border-border p-2" data-rule={isDefault ? "default" : index}>
            <div className="flex items-center justify-between text-sm font-medium">
                {isDefault ? "Otherwise (default)" : `Rule ${index + 1}: when`}
                {stepValue !== undefined && canRemoveRule(stepValue, index) && (
                    <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => {
                            edit((current) => removeRule(current, step, index));
                        }}
                    >
                        Remove rule
                    </Button>
                )}
            </div>
            {conditions.map(([fact, condition]) => (
                <ConditionRow
                    key={fact}
                    fact={fact}
                    condition={condition}
                    facts={facts}
                    onChange={(changed) => {
                        setWhen({ ...rule.when, [fact]: changed });
                    }}
                    onRemove={() => {
                        setWhen(Object.fromEntries(conditions.filter(([other]) => other !== fact)));
                    }}
                    onFact={(name) => {
                        setWhen(
                            Object.fromEntries(
                                conditions.map(([other, value]) => (other === fact ? [name, freshCondition("oneOf")] : [other, value])),
                            ),
                        );
                    }}
                />
            ))}
            {!isDefault && unused[0] !== undefined && (
                <Button
                    type="button"
                    variant="outline"
                    size="xs"
                    onClick={() => {
                        setWhen({ ...rule.when, [unused[0]?.name ?? ""]: freshCondition("oneOf") });
                    }}
                >
                    Add a condition
                </Button>
            )}
            {!isDefault && facts.length === 0 && <p className="text-xs text-muted-foreground">This bot has no facts to compare.</p>}
            <TargetSelect
                label={isDefault ? "Otherwise go to" : "Go to"}
                value={rule.to}
                draft={draft}
                onChange={(to) => {
                    edit((current) => setRuleTarget(current, step, index, to));
                }}
            />
        </li>
    );
}

/** The rules of a step, the default rule last. */
export function RuleEditor({ rules, context }: { rules: Rule[]; context: Context }) {
    return (
        <ul className="flex flex-col gap-2" aria-label="Rules">
            {rules.map((rule, index) => (
                <RuleCard key={`${index}`} rule={rule} index={index} context={context} />
            ))}
        </ul>
    );
}

export { TargetSelect };
