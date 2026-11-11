// The procedure as the editor holds it, and every edit of it. The editor holds no rule of the format: it only keeps the draft in the
// shape the server accepts, and the server's dry run says what is wrong with it.
import type { Fact, ProcedureDetail, SaveProcedure } from "./api";
import {
    armsOf,
    changeList,
    changeRow,
    emptyList,
    fallOf,
    findRow,
    firstStepOf,
    linksOf,
    listAt,
    listOf,
    moveIf,
    moveRow as moveRowIn,
    swapBranch,
    namesIn,
    renamedList,
    withoutRow,
    type Branch,
    type ListEnd,
    type ListRef,
    type StepList,
    type StepRow,
} from "./list";

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

export interface KnowledgeEntry {
    knowledgeBase: string;
    articles?: string[];
    searchQueries?: string[];
}

interface Instruction {
    instruction: string;
    knowledge?: KnowledgeEntry[];
}

/** A whenStalled is an instruction alone: knowledge belongs on an inform. */
export interface StallInstruction {
    instruction: string;
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
    /** Held only by a step under Not connected: the place of a row in the list says where any other step goes. */
    onFailure?: string;
    /** Held only by a step under Not connected, as onFailure is. */
    onUnavailable?: string;
    next?: string | Rule[];
}

export type LocalFact = NonNullable<SaveProcedure["localFacts"]>[string];

export interface Draft {
    description: string;
    /** The steps in reading order: this, not the steps, says what comes after what. */
    list: StepList;
    /** Steps no path from the first step reaches, kept as stored until moved into the list. */
    notConnected: string[];
    whenStalled: StallInstruction | null;
    /** Left out when the author chose nothing, so a save does not write a choice nobody made. */
    whenInterrupted?: string;
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

function informOf(wire: NonNullable<Wire["inform"]>): Inform {
    return {
        instruction: wire.instruction ?? "",
        ...(wire.knowledge
            ? {
                  knowledge: wire.knowledge.map((entry) => ({
                      knowledgeBase: entry.knowledgeBase ?? "",
                      ...(entry.articles ? { articles: [...entry.articles] } : {}),
                      ...(entry.searchQueries ? { searchQueries: [...entry.searchQueries] } : {}),
                  })),
              }
            : {}),
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
    if (wire.onUnavailable != null) step.onUnavailable = wire.onUnavailable;
    const next = wire.next;
    if (typeof next === "string") step.next = next;
    else if (Array.isArray(next)) step.next = next.map(ruleOf);
    return step;
}

export function fromDetail(detail: ProcedureDetail): Draft {
    const stored = Object.fromEntries(Object.entries(detail.steps).map(([name, wire]) => [name, stepOf(wire)]));
    const { list, notConnected } = listOf(detail.firstStep, stored);
    return {
        description: detail.description,
        list,
        notConnected,
        whenStalled: detail.whenStalled ? { instruction: detail.whenStalled.instruction ?? "" } : null,
        steps: Object.fromEntries(
            Object.entries(stored).map(([name, step]) => {
                if (notConnected.includes(name)) return [name, step];
                const { next: _next, onFailure: _onFailure, onUnavailable: _onUnavailable, ...block } = step;
                return [name, block];
            }),
        ),
        localFacts: detail.localFacts ?? {},
        ...(detail.whenInterrupted ? { whenInterrupted: detail.whenInterrupted } : {}),
    };
}

/** What a save sends. Two equal drafts give equal text, so it also tells whether anything changed. */
export function toSave(draft: Draft): SaveProcedure {
    const links = linksOf(draft.list);
    return {
        description: draft.description,
        firstStep: firstStepOf(draft.list),
        whenStalled: draft.whenStalled,
        steps: Object.fromEntries(
            Object.entries(draft.steps).map(([name, step]) => {
                const linked = links[name];
                const next = linked ? linked.next : step.next;
                const onFailure = linked ? linked.onFailure : step.onFailure;
                const onUnavailable = linked ? linked.onUnavailable : step.onUnavailable;
                return [
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
                        ...(onFailure === undefined ? {} : { onFailure }),
                        ...(onUnavailable === undefined ? {} : { onUnavailable }),
                        ...(next === undefined ? {} : { next }),
                    },
                ];
            }),
        ),
        ...(Object.keys(draft.localFacts).length === 0 ? {} : { localFacts: draft.localFacts }),
        ...(draft.whenInterrupted ? { whenInterrupted: draft.whenInterrupted } : {}),
    };
}

function uniqueName(draft: Draft, base: string): string {
    if (!(base in draft.steps)) return base;
    let number = 2;
    while (`${base}${number}` in draft.steps) number++;
    return `${base}${number}`;
}

const EMPTY_BLOCK: Record<BlockName, Step> = {
    listen: { listen: true },
    collect: { collect: [] },
    say: { say: "" },
    assign: { assign: {} },
    start: { start: "" },
    inform: { inform: { instruction: "" } },
    action: { action: "" },
};

/** Adds a step at the end of a list, above a Next: or End row if the list has one. Its name comes from its block and is free to change. */
export function addStep(draft: Draft, ref: ListRef, block: BlockName): { draft: Draft; name: string } {
    const name = uniqueName(draft, block);
    return {
        draft: {
            ...draft,
            steps: { ...draft.steps, [name]: EMPTY_BLOCK[block] },
            list: changeList(draft.list, ref, (one) => ({ ...one, rows: [...one.rows, { name }] })),
        },
        name,
    };
}

export function setListEnd(draft: Draft, ref: ListRef, last: ListEnd): Draft {
    return { ...draft, list: changeList(draft.list, ref, (one) => ({ ...one, last })) };
}

/** End says a step has no next, which is what the end of a list already means when nothing follows, so it is only offered where a row would. */
export function canEnd(draft: Draft, ref: ListRef): boolean {
    // Why: an empty When unavailable means none, so an End with no step above it would say nothing.
    if (ref?.arm === "unavailable" && listAt(draft.list, ref).rows.length === 0) return false;
    return ref !== null && fallOf(draft.list, ref) !== null;
}

/** An If hangs on the step above it, so only a step with no If and no branches of its own can take one. */
export function canTakeIf(row: StepRow | undefined): boolean {
    return row !== undefined && row.branches === undefined;
}

/** A new If comes with its Else. On an action it takes the place of the fixed next, keeping what that branch held as its Else. */
export function addIf(draft: Draft, name: string): Draft {
    return {
        ...draft,
        list: changeRow(draft.list, name, (row) => {
            const { success, ...rest } = row;
            return { ...rest, branches: [{ when: {}, list: emptyList() }, { list: success ?? emptyList() }] };
        }),
    };
}

/** How many steps removing an If or a branch takes with it: the author is asked first when there are any. */
export function stepsInBranches(row: StepRow | undefined): number {
    return (row?.branches ?? []).flatMap((branch) => namesIn(branch.list)).length;
}

export function stepsInBranch(branch: Branch): number {
    return namesIn(branch.list).length;
}

function withoutSteps(steps: Record<string, Step>, names: string[]): Record<string, Step> {
    return Object.fromEntries(Object.entries(steps).filter(([name]) => !names.includes(name)));
}

export function removeIf(draft: Draft, name: string): Draft {
    const row = findRow(draft.list, name);
    const gone = (row?.branches ?? []).flatMap((branch) => namesIn(branch.list));
    return {
        ...draft,
        steps: withoutSteps(draft.steps, gone),
        list: changeRow(draft.list, name, (found) => {
            const { branches: _, ...rest } = found;
            return found.failure || found.unavailable ? { ...rest, success: emptyList() } : rest;
        }),
    };
}

/** Adds a branch before the Else, whose rule must stay last. */
export function addElseIf(draft: Draft, name: string): Draft {
    return {
        ...draft,
        list: changeRow(draft.list, name, (row) => {
            const branches = row.branches ?? [];
            const fresh: Branch = { when: {}, list: emptyList() };
            const last = branches[branches.length - 1];
            return { ...row, branches: last && last.when === undefined ? [...branches.slice(0, -1), fresh, last] : [...branches, fresh] };
        }),
    };
}

export function addElse(draft: Draft, name: string): Draft {
    return {
        ...draft,
        list: changeRow(draft.list, name, (row) => ({ ...row, branches: [...(row.branches ?? []), { list: emptyList() }] })),
    };
}

/** Removes a branch and its steps. With only the Else left there is nothing to choose, so its rows simply follow the step. */
export function removeBranch(draft: Draft, name: string, index: number): Draft {
    const row = findRow(draft.list, name);
    const branch = row?.branches?.[index];
    if (row === undefined || branch?.when === undefined) return draft;
    const gone = namesIn(branch.list);
    const left = (row.branches ?? []).filter((_, at) => at !== index);
    const only = left.length === 1 ? left[0] : undefined;
    const steps = withoutSteps(draft.steps, gone);
    if (only === undefined || only.when !== undefined) {
        return { ...draft, steps, list: changeRow(draft.list, name, (found) => ({ ...found, branches: left })) };
    }
    const { branches: _, ...plain } = row;
    const replaced = changeList(draft.list, ownerRef(draft.list, name), (one) => {
        const at = one.rows.findIndex((other) => other.name === name);
        return {
            ...one,
            rows: [...one.rows.slice(0, at), plain, ...only.list.rows, ...one.rows.slice(at + 1)],
            last: at === one.rows.length - 1 && only.list.last !== null ? only.list.last : one.last,
        };
    });
    return { ...draft, steps, list: replaced };
}

function ownerRef(list: StepList, name: string, ref: ListRef = null): ListRef {
    if (list.rows.some((row) => row.name === name)) return ref;
    for (const row of list.rows) {
        for (const arm of armsOf(row)) {
            if (namesIn(arm.list).includes(name)) return ownerRef(arm.list, name, { step: row.name, arm: arm.arm });
        }
    }
    return ref;
}

export function setConditions(draft: Draft, name: string, index: number, when: Record<string, Condition>): Draft {
    return {
        ...draft,
        list: changeRow(draft.list, name, (row) => ({
            ...row,
            branches: (row.branches ?? []).map((branch, at) => (at === index ? { ...branch, when } : branch)),
        })),
    };
}

/** An action's On failure, opened by the author; empty, it goes on below. */
export function openFailure(draft: Draft, name: string): Draft {
    return {
        ...draft,
        list: changeRow(draft.list, name, (row) => ({
            ...row,
            failure: row.failure ?? emptyList(),
            ...(row.branches === undefined ? { success: row.success ?? emptyList() } : {}),
        })),
    };
}

/** Renames a step and every place that points at it. A taken or empty name changes nothing. */
export function renameStep(draft: Draft, from: string, to: string): Draft {
    if (to === from || to === "" || to in draft.steps || !(from in draft.steps)) return draft;
    const again = (name: string) => (name === from ? to : name);
    return {
        ...draft,
        list: renamedList(draft.list, from, to),
        notConnected: draft.notConnected.map(again),
        steps: Object.fromEntries(
            Object.entries(draft.steps).map(([name, step]) => [
                again(name),
                {
                    ...step,
                    ...(step.onFailure === undefined ? {} : { onFailure: again(step.onFailure) }),
                    ...(step.onUnavailable === undefined ? {} : { onUnavailable: again(step.onUnavailable) }),
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

/** Why a step cannot be deleted yet, if so: an If needs the step above it, and what hangs there is the author's to remove first. */
export function whyNotDeletable(row: StepRow | undefined): string | null {
    if (row === undefined) return null;
    if (row.branches) return "Remove the If below this step first: an If needs a step above it.";
    if (armsOf(row).some((arm) => typeof arm.arm === "string" && namesIn(arm.list).length > 0)) {
        return "Remove the steps in its branches first.";
    }
    return null;
}

/** Deletes a step: the rows around it join up. A Next: row naming it stays, and the server's dry run says what that leaves to fix. */
export function deleteStep(draft: Draft, name: string): Draft {
    const row = findRow(draft.list, name);
    if (whyNotDeletable(row) !== null) return draft;
    return {
        ...draft,
        steps: withoutSteps(draft.steps, [name]),
        notConnected: draft.notConnected.filter((other) => other !== name),
        list: withoutRow(draft.list, name),
    };
}

export function moveStep(draft: Draft, name: string, direction: "up" | "down"): Draft {
    return { ...draft, list: moveRowIn(draft.list, name, direction) };
}

export function moveIfBlock(draft: Draft, name: string, direction: "up" | "down"): Draft {
    return { ...draft, list: moveIf(draft.list, name, direction) };
}

export function moveBranch(draft: Draft, name: string, index: number, direction: "up" | "down"): Draft {
    return { ...draft, list: swapBranch(draft.list, name, index, direction) };
}

/** Moves a Not connected step to the end of the main list; its place says where it goes now, so what it stored goes. */
export function connectStep(draft: Draft, name: string): Draft {
    const { next: _next, onFailure: _onFailure, onUnavailable: _onUnavailable, ...block } = draft.steps[name] ?? {};
    return {
        ...draft,
        notConnected: draft.notConnected.filter((other) => other !== name),
        steps: { ...draft.steps, [name]: block },
        list: { ...draft.list, rows: [...draft.list.rows, { name }] },
    };
}

export function setBlock(draft: Draft, name: string, change: Partial<Step>): Draft {
    if (!(name in draft.steps)) return draft;
    return { ...draft, steps: { ...draft.steps, [name]: { ...draft.steps[name], ...change } } };
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
        pattern: fact.pattern ?? null,
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

/** An article an entry may name: the id is what is stored, the title what the author sees, and the base decides where it may be picked. */
export interface LinkableArticle {
    id: string;
    title: string;
    knowledgeBase: string;
}

export interface LinkableBase {
    id: string;
    title: string;
}

/** The text of a whenStalled as typed: emptying it removes the instruction. */
export function withStallText(text: string): StallInstruction | null {
    return text === "" ? null : { instruction: text };
}

/** The bases an instruction can still name, since a second entry for one is a mistake. */
export function basesOffered(bases: LinkableBase[], knowledge: KnowledgeEntry[], keep?: string): LinkableBase[] {
    return bases.filter((one) => one.id === keep || !knowledge.some((entry) => entry.knowledgeBase === one.id));
}

export function articlesOf(articles: LinkableArticle[], knowledgeBase: string): LinkableArticle[] {
    return articles.filter((article) => article.knowledgeBase === knowledgeBase);
}

/** An article belongs to one base, so changing the base clears them; the queries are only text and stay. */
export function withBase(entry: KnowledgeEntry, knowledgeBase: string): KnowledgeEntry {
    const { articles: _, ...rest } = entry;
    return { ...rest, knowledgeBase };
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
