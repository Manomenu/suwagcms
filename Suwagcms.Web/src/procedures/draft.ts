// The procedure as the creator edits it, and every edit of it. The creator holds no rule of the format: it only keeps the draft
// in the shape the server accepts, and the server's dry run says what is wrong with it.
import type { Fact, ProcedureDetail, SaveProcedure } from "./api";

export type BlockName = "listen" | "collect" | "say" | "assign" | "start" | "inform" | "action";
export const BLOCKS: readonly BlockName[] = ["listen", "collect", "say", "assign", "start", "inform", "action"];

export interface Condition {
    oneOf?: string[];
    range?: { min?: number; max?: number };
    known?: boolean;
}

export type ConditionKind = keyof Condition;

export interface Rule {
    /** Absent on the default rule, which is always the last. */
    when?: Record<string, Condition>;
    to: string;
}

export interface Context {
    articles?: string[];
    searchQueries?: string[];
}

export interface Instruction {
    instruction: string;
    context?: Context;
}

export interface Inform extends Instruction {
    points?: Record<string, string>;
}

export interface Step {
    listen?: true;
    collect?: string[];
    say?: string;
    assign?: Record<string, string>;
    /** The id of the procedure this step starts. */
    start?: string;
    inform?: Inform;
    /** The id of the action this step runs; "" until one is chosen. */
    action?: string;
    /** Parameter name to the fact that gives its value. */
    with?: Record<string, string>;
    /** Result name to the fact it writes. */
    results?: Record<string, string>;
    /** The step to go to when the action fails. */
    onFailure?: string;
    /** A step's name, or rules whose last one is the default. A target of "" is a rule not yet connected. */
    next?: string | Rule[];
}

export type LocalFact = NonNullable<SaveProcedure["localFacts"]>[string];

export interface Draft {
    description: string;
    firstStep: string | null;
    whenStalled: Instruction | null;
    steps: Record<string, Step>;
    localFacts: Record<string, LocalFact>;
}

type Wire = ProcedureDetail["steps"][string];

function ruleOf(raw: unknown): Rule {
    const rule = raw as { when?: Record<string, Record<string, unknown> | null> | null; to?: string };
    const rest: Rule = { to: rule.to ?? "" };
    if (rule.when) {
        rest.when = Object.fromEntries(
            Object.entries(rule.when).map(([fact, condition]) => {
                const given: Condition = {};
                if (condition?.["oneOf"]) given.oneOf = condition["oneOf"] as string[];
                if (condition?.["range"]) {
                    const range = condition["range"] as { min?: number | null; max?: number | null };
                    given.range = { ...(range.min == null ? {} : { min: range.min }), ...(range.max == null ? {} : { max: range.max }) };
                }
                if (condition?.["known"] != null) given.known = condition["known"] as boolean;
                return [fact, given];
            }),
        );
    }
    return rest;
}

function instructionOf(wire: NonNullable<ProcedureDetail["whenStalled"]>): Instruction {
    const context = wire.context;
    return {
        instruction: wire.instruction ?? "",
        ...(context
            ? {
                  context: {
                      ...(context.articles ? { articles: [...context.articles] } : {}),
                      ...(context.searchQueries ? { searchQueries: [...context.searchQueries] } : {}),
                  },
              }
            : {}),
    };
}

function informOf(wire: NonNullable<Wire["inform"]>): Inform {
    return {
        ...instructionOf({ instruction: wire.instruction ?? "", context: wire.context ?? null }),
        ...(wire.points ? { points: { ...wire.points } } : {}),
    };
}

function stepOf(wire: Wire): Step {
    const step: Step = {};
    if (wire.listen) step.listen = true;
    if (wire.collect) step.collect = [...wire.collect];
    if (wire.say != null) step.say = wire.say;
    if (wire.assign) step.assign = { ...wire.assign };
    if (wire.start != null) step.start = wire.start;
    if (wire.inform) step.inform = informOf(wire.inform);
    if (wire.action != null) step.action = wire.action;
    if (wire.with) step.with = { ...wire.with };
    if (wire.results) step.results = { ...wire.results };
    if (wire.onFailure != null) step.onFailure = wire.onFailure;
    const next = wire.next;
    if (typeof next === "string") step.next = next;
    else if (Array.isArray(next)) step.next = next.map(ruleOf);
    return step;
}

export function fromDetail(detail: ProcedureDetail): Draft {
    return {
        description: detail.description,
        firstStep: detail.firstStep,
        whenStalled: detail.whenStalled ? instructionOf(detail.whenStalled) : null,
        steps: Object.fromEntries(Object.entries(detail.steps).map(([name, wire]) => [name, stepOf(wire)])),
        localFacts: detail.localFacts ?? {},
    };
}

/** What a save sends. Two equal drafts give equal text, so it also tells whether anything changed. */
export function toSave(draft: Draft): SaveProcedure {
    return {
        description: draft.description,
        firstStep: draft.firstStep,
        whenStalled: draft.whenStalled,
        steps: Object.fromEntries(
            Object.entries(draft.steps).map(([name, step]) => [
                name,
                {
                    ...(step.listen ? { listen: {} } : {}),
                    ...(step.collect ? { collect: step.collect } : {}),
                    ...(step.say === undefined ? {} : { say: step.say }),
                    ...(step.assign ? { assign: step.assign } : {}),
                    ...(step.start === undefined ? {} : { start: step.start }),
                    ...(step.inform ? { inform: step.inform } : {}),
                    ...(step.action === undefined ? {} : { action: step.action }),
                    ...(step.with ? { with: step.with } : {}),
                    ...(step.results ? { results: step.results } : {}),
                    ...(step.onFailure === undefined ? {} : { onFailure: step.onFailure }),
                    ...(step.next === undefined ? {} : { next: step.next }),
                },
            ]),
        ),
        ...(Object.keys(draft.localFacts).length === 0 ? {} : { localFacts: draft.localFacts }),
    };
}

function targetsOf(step: Step): string[] {
    if (step.next === undefined) return [];
    return typeof step.next === "string" ? [step.next] : step.next.map((rule) => rule.to);
}

/**
 * A stable order for the steps: from the first step along the edges, then the ones nothing reaches by name. The server sends steps
 * in the order its database keeps them, which is not the author's, so the graph is the only order there is.
 */
export function stepOrder(draft: Draft): string[] {
    const seen = new Set<string>();
    const queue = draft.firstStep !== null && draft.firstStep in draft.steps ? [draft.firstStep] : [];
    while (queue.length > 0) {
        const name = queue.shift();
        if (name === undefined || seen.has(name) || !(name in draft.steps)) continue;
        seen.add(name);
        const step = draft.steps[name] ?? {};
        queue.push(...targetsOf(step), ...(step.onFailure === undefined ? [] : [step.onFailure]));
    }
    const rest = Object.keys(draft.steps)
        .filter((name) => !seen.has(name))
        .sort();
    return [...seen, ...rest];
}

export const FAILURE = "failure";

export interface Edge {
    id: string;
    source: string;
    /** `next` for a fixed next, `rule-<n>` for the rule at that position, `failure` for an action's onFailure. */
    handle: string;
    target: string;
}

export function handlesOf(step: Step): string[] {
    return Array.isArray(step.next) ? step.next.map((_, index) => `rule-${index}`) : ["next"];
}

export function edgesOf(draft: Draft): Edge[] {
    return stepOrder(draft).flatMap((source) => {
        const step = draft.steps[source] ?? {};
        const failure = step.onFailure !== undefined && step.onFailure in draft.steps ? step.onFailure : null;
        return [
            ...targetsOf(step).flatMap((target, index) => {
                if (!(target in draft.steps)) return [];
                const handle = handlesOf(step)[index] ?? "next";
                return [{ id: `${source}:${handle}`, source, handle, target }];
            }),
            ...(failure === null ? [] : [{ id: `${source}:${FAILURE}`, source, handle: FAILURE, target: failure }]),
        ];
    });
}

function withStep(draft: Draft, name: string, change: (step: Step) => Step): Draft {
    if (!(name in draft.steps)) return draft;
    return { ...draft, steps: { ...draft.steps, [name]: change(draft.steps[name] ?? {}) } };
}

function ruleIndex(handle: string): number | null {
    const match = /^rule-(\d+)$/.exec(handle);
    return match?.[1] === undefined ? null : Number(match[1]);
}

/** Draws an edge: sets the step's fixed next, or the target of the rule the handle belongs to. */
export function connect(draft: Draft, source: string, handle: string, target: string): Draft {
    const index = ruleIndex(handle);
    return withStep(draft, source, (step) => {
        if (handle === FAILURE) return { ...step, onFailure: target };
        if (index !== null && Array.isArray(step.next)) {
            return { ...step, next: step.next.map((rule, at) => (at === index ? { ...rule, to: target } : rule)) };
        }
        return { ...step, next: target };
    });
}

/** Removes an edge; a rule stays, with no target yet, so the author does not lose its condition. */
export function disconnect(draft: Draft, source: string, handle: string): Draft {
    const index = ruleIndex(handle);
    return withStep(draft, source, (step) => {
        if (handle === FAILURE) {
            const { onFailure: _, ...rest } = step;
            return rest;
        }
        if (index !== null && Array.isArray(step.next)) {
            return { ...step, next: step.next.map((rule, at) => (at === index ? { ...rule, to: "" } : rule)) };
        }
        const { next: _, ...rest } = step;
        return rest;
    });
}

function uniqueName(draft: Draft, base: string): string {
    if (!(base in draft.steps)) return base;
    let number = 2;
    while (`${base}${number}` in draft.steps) number++;
    return `${base}${number}`;
}

export function addStep(draft: Draft, block: BlockName): { draft: Draft; name: string } {
    const name = uniqueName(draft, block);
    const empty: Record<BlockName, Step> = {
        listen: { listen: true },
        collect: { collect: [] },
        say: { say: "" },
        assign: { assign: {} },
        start: { start: "" },
        inform: { inform: { instruction: "" } },
        action: { action: "" },
    };
    return { draft: { ...draft, steps: { ...draft.steps, [name]: empty[block] } }, name };
}

/** Renames a step and every place that points at it. A taken or empty name changes nothing. */
export function renameStep(draft: Draft, from: string, to: string): Draft {
    if (to === from || to === "" || to in draft.steps || !(from in draft.steps)) return draft;
    const again = (name: string) => (name === from ? to : name);
    return {
        ...draft,
        firstStep: draft.firstStep === null ? null : again(draft.firstStep),
        steps: Object.fromEntries(
            Object.entries(draft.steps).map(([name, step]) => [
                again(name),
                {
                    ...step,
                    ...(step.onFailure === undefined ? {} : { onFailure: again(step.onFailure) }),
                    ...(step.next === undefined
                        ? {}
                        : {
                              next:
                                  typeof step.next === "string"
                                      ? again(step.next)
                                      : step.next.map((rule) => ({ ...rule, to: again(rule.to) })),
                          }),
                },
            ]),
        ),
    };
}

/** Deletes a step with every edge and rule target that points at it. What that leaves unfinished is for the server to say. */
export function deleteStep(draft: Draft, name: string): Draft {
    const rest = Object.fromEntries(
        Object.entries(draft.steps)
            .filter(([other]) => other !== name)
            .map(([other, full]) => {
                const { onFailure, ...others } = full;
                const step: Step = onFailure === undefined || onFailure === name ? others : full;
                if (typeof step.next === "string" && step.next === name) {
                    const { next: _, ...without } = step;
                    return [other, without];
                }
                if (Array.isArray(step.next))
                    return [other, { ...step, next: step.next.map((rule) => (rule.to === name ? { ...rule, to: "" } : rule)) }];
                return [other, step];
            }),
    );
    return { ...draft, firstStep: draft.firstStep === name ? null : draft.firstStep, steps: rest };
}

export function markFirst(draft: Draft, name: string): Draft {
    return { ...draft, firstStep: name };
}

export function setBlock(draft: Draft, name: string, change: Partial<Step>): Draft {
    return withStep(draft, name, (step) => ({ ...step, ...change }));
}

/** The first rule added to a step splits its next in two: the rule, and the default rule that keeps the old target. */
export function addRule(draft: Draft, name: string): Draft {
    return withStep(draft, name, (step) => {
        const fresh: Rule = { when: {}, to: "" };
        if (Array.isArray(step.next)) return { ...step, next: [...step.next.slice(0, -1), fresh, ...step.next.slice(-1)] };
        return { ...step, next: [fresh, { to: step.next ?? "" }] };
    });
}

/** The default rule, the last, cannot be removed while others remain; removing the last other rule makes it a fixed next again. */
export function canRemoveRule(step: Step, index: number): boolean {
    return Array.isArray(step.next) && step.next.length > 1 && index < step.next.length - 1;
}

export function removeRule(draft: Draft, name: string, index: number): Draft {
    return withStep(draft, name, (step) => {
        if (!canRemoveRule(step, index) || !Array.isArray(step.next)) return step;
        const rules = step.next.filter((_, at) => at !== index);
        const only = rules.length === 1 ? rules[0] : undefined;
        if (only !== undefined) {
            const { next: _, ...rest } = step;
            return only.to === "" ? rest : { ...rest, next: only.to };
        }
        return { ...step, next: rules };
    });
}

export function setRuleTarget(draft: Draft, name: string, index: number, to: string): Draft {
    return connect(draft, name, `rule-${index}`, to);
}

export function setConditions(draft: Draft, name: string, index: number, when: Record<string, Condition>): Draft {
    return withStep(draft, name, (step) =>
        Array.isArray(step.next) ? { ...step, next: step.next.map((rule, at) => (at === index ? { ...rule, when } : rule)) } : step,
    );
}

/** Which kinds of condition a fact takes: `range` only for a whole number; `oneOf` offers an enum's values. */
export function conditionKinds(fact: Pick<Fact, "type"> | undefined): ConditionKind[] {
    return fact?.type === "int" ? ["oneOf", "range", "known"] : ["oneOf", "known"];
}

export function freshCondition(kind: ConditionKind): Condition {
    return kind === "oneOf" ? { oneOf: [] } : kind === "range" ? { range: {} } : { known: true };
}

export function kindOf(condition: Condition): ConditionKind {
    return condition.range ? "range" : condition.known !== undefined ? "known" : "oneOf";
}

/** What a step of this procedure sees: the bot's facts and the procedure's own, which only it can name. */
export function factsSeen(draft: Draft, facts: Fact[]): Fact[] {
    const local = Object.entries(draft.localFacts).map(([name, fact]) => ({
        name,
        type: fact.type ?? "",
        description: fact.description ?? "",
        writableBy: fact.writableBy ?? "set",
        values: fact.values ?? null,
        min: fact.min ?? null,
        max: fact.max ?? null,
        maxLength: fact.maxLength ?? null,
        revision: 0,
        usedBy: [],
    }));
    return [...facts, ...local];
}

/** Adds a local fact, or replaces it; the server's dry run says what is wrong with it. */
export function setLocalFact(draft: Draft, name: string, fact: LocalFact): Draft {
    return { ...draft, localFacts: { ...draft.localFacts, [name]: fact } };
}

/** Removes a local fact; steps that still use it are for the server's dry run to list. */
export function removeLocalFact(draft: Draft, name: string): Draft {
    return { ...draft, localFacts: Object.fromEntries(Object.entries(draft.localFacts).filter(([other]) => other !== name)) };
}

/** The facts a collect may use: a fact only a procedure assigns is not asked of the customer. */
export function collectable(facts: Fact[]): Fact[] {
    return facts.filter((fact) => fact.writableBy !== "assign");
}

/** An article a block or a context may name: the id is what is stored, the title what the author sees. */
export interface LinkableArticle {
    id: string;
    title: string;
}

/** The text of a whenStalled as typed: emptying it removes the instruction, unless a context would be lost with it. */
export function withStallText(whenStalled: Instruction | null, text: string): Instruction | null {
    if (text === "" && !whenStalled?.context) return null;
    return { ...whenStalled, instruction: text };
}

export function articleLabel(articles: LinkableArticle[], id: string): string {
    const title = articles.find((article) => article.id === id)?.title;
    return title === undefined ? id : `${title} (${id})`;
}

/** The procedures a start may target: not the base of every conversation, and not the procedure itself, which would be a loop. */
export function startable(procedures: string[], self: string): string[] {
    return procedures.filter((id) => id !== "conversation" && id !== self);
}

/** What a form lists: what it offers, then whatever the step already names, so opening a procedure never hides what is stored. */
export function withStored(offered: string[], stored: string[]): string[] {
    return [...offered, ...stored.filter((name) => !offered.includes(name))];
}

export function valuesOffered(fact: Pick<Fact, "type" | "values"> | undefined): string[] | null {
    return fact?.type === "enum" ? (fact.values ?? []) : null;
}

type ContractField = {
    [Key in "values" | "min" | "max" | "maxLength"]?: Fact[Key] | undefined;
} & { type?: string | undefined };

function holds(target: ContractField, source: ContractField): boolean {
    if (source.type !== target.type) return false;
    if (source.type === "enum") return (source.values ?? []).every((value) => (target.values ?? []).includes(value));
    if (source.type === "int") return Number(source.min) >= Number(target.min) && Number(source.max) <= Number(target.max);
    if (source.type === "text") return Number(source.maxLength) <= Number(target.maxLength);
    return true;
}

/** The facts a row of an action step offers: every value the source can hold must fit the target, so a parameter takes from a fact and a result writes to one. */
export function factsThatFit(kind: "with" | "results", field: ContractField, facts: Fact[]): Fact[] {
    return facts.filter((fact) => (kind === "with" ? holds(field, fact) : holds(fact, field)));
}

function freePointName(points: Record<string, string>): string {
    let number = Object.keys(points).length + 1;
    while (`point${number}` in points) number++;
    return `point${number}`;
}

export function addPoint(points: Record<string, string>): Record<string, string> {
    return { ...points, [freePointName(points)]: "" };
}

// Why: a name another point already has is not taken, because the map could not hold both.
export function renamePoint(points: Record<string, string>, name: string, newName: string): Record<string, string> {
    if (newName in points) return points;
    return Object.fromEntries(Object.entries(points).map(([key, text]) => [key === name ? newName : key, text]));
}

export function removePoint(points: Record<string, string>, name: string): Record<string, string> {
    return Object.fromEntries(Object.entries(points).filter(([key]) => key !== name));
}
