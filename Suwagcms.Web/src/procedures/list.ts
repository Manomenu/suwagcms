import type { Condition, Rule, Step } from "./draft";

export type ListEnd = null | "end" | { next: string };

export interface Branch {
    when?: Record<string, Condition>;
    list: StepList;
}

export interface StepRow {
    name: string;
    branches?: Branch[];
    failure?: StepList;
    unavailable?: StepList;
    success?: StepList;
}

export interface StepList {
    rows: StepRow[];
    last: ListEnd;
}

export type Links = Pick<Step, "next" | "onFailure" | "onUnavailable">;

type Arm = number | "failure" | "unavailable" | "success";

export function emptyList(): StepList {
    return { rows: [], last: null };
}

function loneDefault(next: Step["next"]): string | Rule[] | undefined {
    return Array.isArray(next) && next.length === 1 && next[0]?.when === undefined ? next[0]?.to : next;
}

function targetsOf(step: Step): string[] {
    const next = loneDefault(step.next);
    const targets = next === undefined ? [] : typeof next === "string" ? [next] : next.map((rule) => rule.to);
    return [...targets, ...[step.onFailure, step.onUnavailable].filter((target) => target !== undefined)];
}

function known(steps: Record<string, Step>, name: string): boolean {
    return Object.hasOwn(steps, name);
}

function backEdges(steps: Record<string, Step>, first: string): Set<string> {
    const back = new Set<string>();
    const state = new Map<string, "open" | "done">([[first, "open"]]);
    const stack = [{ name: first, targets: targetsOf(steps[first] ?? {}).filter((target) => known(steps, target)), at: 0 }];
    while (stack.length > 0) {
        const top = stack[stack.length - 1];
        if (top === undefined) break;
        const target = top.targets[top.at];
        if (target === undefined) {
            state.set(top.name, "done");
            stack.pop();
            continue;
        }
        top.at++;
        const seen = state.get(target);
        if (seen === undefined) {
            state.set(target, "open");
            stack.push({ name: target, targets: targetsOf(steps[target] ?? {}).filter((other) => known(steps, other)), at: 0 });
        } else if (seen === "open") {
            back.add(`${top.name}\n${target}`);
        }
    }
    return back;
}

export function listOf(firstStep: string | null, steps: Record<string, Step>): { list: StepList; notConnected: string[] } {
    if (firstStep === null || !known(steps, firstStep)) return { list: emptyList(), notConnected: Object.keys(steps).sort() };
    const back = backEdges(steps, firstStep);
    const placed = new Set<string>();

    const forward = (name: string) =>
        targetsOf(steps[name] ?? {}).filter((target) => known(steps, target) && !back.has(`${name}\n${target}`));

    const reach = (from: string): string[] => {
        const seen: string[] = [];
        const queue = known(steps, from) ? [from] : [];
        for (let name = queue.shift(); name !== undefined; name = queue.shift()) {
            if (seen.includes(name) || placed.has(name)) continue;
            seen.push(name);
            queue.push(...forward(name));
        }
        return seen;
    };

    const continuationOf = (targets: string[], stop: string | null, outer: Set<string>): string | null => {
        const reached = targets.map(reach);
        const order = [...new Set(reached.flat())];
        const shared = order.filter((name) => reached.filter((one) => one.includes(name)).length >= 2);
        const first = shared.find((name) => !shared.some((other) => other !== name && reach(other).includes(name)));
        if (first === undefined || (first !== stop && outer.has(first))) return null;
        return first;
    };

    const walk = (start: string | null, stop: string | null, outer: Set<string>): StepList => {
        const rows: StepRow[] = [];
        let current = start;
        while (current !== null && current !== stop) {
            if (placed.has(current) || outer.has(current) || !known(steps, current)) return { rows, last: { next: current } };
            placed.add(current);
            const name: string = current;
            const step = steps[name] ?? {};
            const next = loneDefault(step.next);
            const failure = step.onFailure;
            const unavailable = step.onUnavailable;
            const rules = Array.isArray(next) ? next : null;
            if (failure === undefined && unavailable === undefined && rules === null) {
                rows.push({ name });
                if (next === undefined) return { rows, last: stop === null ? null : "end" };
                current = next as string;
                continue;
            }
            const targets = [...(rules ?? []).map((rule) => rule.to), ...(typeof next === "string" ? [next] : [])];
            const arms = [failure, unavailable].filter((target) => target !== undefined);
            const continuation = continuationOf([...targets, ...arms], stop, outer);
            const inner = continuation ?? stop;
            const deeper = new Set([...outer, ...(stop === null ? [] : [stop]), ...(continuation === null ? [] : [continuation])]);
            if (inner !== null) deeper.delete(inner);
            const row: StepRow = { name };
            if (failure !== undefined) row.failure = walk(failure, inner, deeper);
            if (unavailable !== undefined) {
                const list = walk(unavailable, inner, deeper);
                row.unavailable = list.rows.length === 0 && list.last === null ? { rows: [], last: { next: unavailable } } : list;
            }
            if (rules !== null) {
                row.branches = rules.map((rule) => ({
                    ...(rule.when === undefined ? {} : { when: rule.when }),
                    list: walk(rule.to, inner, deeper),
                }));
            } else if (typeof next === "string") {
                row.success = walk(next, inner, deeper);
            } else {
                row.success = { rows: [], last: inner === null ? null : "end" };
            }
            rows.push(row);
            if (continuation === null) return { rows, last: null };
            current = continuation;
        }
        return { rows, last: null };
    };

    const list = walk(firstStep, null, new Set());
    return { list, notConnected: Object.keys(steps).filter((name) => !placed.has(name)) };
}

function afterEnd(last: ListEnd, fall: string | null): string | null {
    if (last === "end") return null;
    return last === null ? fall : last.next;
}

function headOf(list: StepList, fall: string | null): string | null {
    return list.rows[0]?.name ?? afterEnd(list.last, fall);
}

export function firstStepOf(list: StepList): string | null {
    return headOf(list, null);
}

export function linksOf(list: StepList): Record<string, Links> {
    const out: Record<string, Links> = {};
    const build = (one: StepList, fall: string | null) => {
        one.rows.forEach((row, index) => {
            const after = one.rows[index + 1]?.name ?? afterEnd(one.last, fall);
            const links: Links = {};
            if (row.failure) {
                const onFailure = headOf(row.failure, after);
                if (onFailure !== null) links.onFailure = onFailure;
                build(row.failure, after);
            }
            if (row.unavailable) {
                const { rows, last } = row.unavailable;
                const onUnavailable = rows[0]?.name ?? (last !== null && last !== "end" ? last.next : null);
                if (onUnavailable !== null) links.onUnavailable = onUnavailable;
                build(row.unavailable, after);
            }
            if (row.branches) {
                links.next = row.branches.map((branch) => ({
                    ...(branch.when === undefined ? {} : { when: branch.when }),
                    to: headOf(branch.list, after) ?? "",
                }));
                for (const branch of row.branches) build(branch.list, after);
            } else {
                const target = row.success ? headOf(row.success, after) : after;
                if (target !== null) links.next = target;
                if (row.success) build(row.success, after);
            }
            out[row.name] = links;
        });
    };
    build(list, null);
    return out;
}

export type ListRef = null | { step: string; arm: Arm };

export function armsOf(row: StepRow): { arm: Arm; list: StepList }[] {
    return [
        ...(row.failure ? [{ arm: "failure" as const, list: row.failure }] : []),
        ...(row.unavailable ? [{ arm: "unavailable" as const, list: row.unavailable }] : []),
        ...(row.success ? [{ arm: "success" as const, list: row.success }] : []),
        ...(row.branches ?? []).map((branch, arm) => ({ arm, list: branch.list })),
    ];
}

function withArm(row: StepRow, arm: Arm, list: StepList): StepRow {
    if (arm === "failure") return { ...row, failure: list };
    if (arm === "unavailable") return { ...row, unavailable: list };
    if (arm === "success") return { ...row, success: list };
    return { ...row, branches: (row.branches ?? []).map((branch, at) => (at === arm ? { ...branch, list } : branch)) };
}

export function namesIn(list: StepList): string[] {
    return list.rows.flatMap((row) => [row.name, ...armsOf(row).flatMap((arm) => namesIn(arm.list))]);
}

function mapRows(list: StepList, change: (row: StepRow) => StepRow): StepList {
    return {
        ...list,
        rows: list.rows.map((row) => {
            const changed = change(row);
            return armsOf(changed).reduce((done, arm) => withArm(done, arm.arm, mapRows(arm.list, change)), changed);
        }),
    };
}

export function changeRow(list: StepList, name: string, change: (row: StepRow) => StepRow): StepList {
    return mapRows(list, (row) => (row.name === name ? change(row) : row));
}

export function findRow(list: StepList, name: string): StepRow | undefined {
    for (const row of list.rows) {
        if (row.name === name) return row;
        for (const arm of armsOf(row)) {
            const found = findRow(arm.list, name);
            if (found) return found;
        }
    }
    return undefined;
}

export function listAt(list: StepList, ref: ListRef): StepList {
    if (ref === null) return list;
    const row = findRow(list, ref.step);
    return (row && armsOf(row).find((arm) => arm.arm === ref.arm)?.list) ?? emptyList();
}

export function changeList(list: StepList, ref: ListRef, change: (one: StepList) => StepList): StepList {
    if (ref === null) return change(list);
    return changeRow(list, ref.step, (row) => {
        const arm = armsOf(row).find((other) => other.arm === ref.arm);
        if (arm) return withArm(row, arm.arm, change(arm.list));
        return ref.arm === "unavailable" ? withArm(row, "unavailable", change(emptyList())) : row;
    });
}

function ownerOf(list: StepList, name: string, ref: ListRef = null): { ref: ListRef; index: number } | null {
    const index = list.rows.findIndex((row) => row.name === name);
    if (index >= 0) return { ref, index };
    for (const row of list.rows) {
        for (const arm of armsOf(row)) {
            const found = ownerOf(arm.list, name, { step: row.name, arm: arm.arm });
            if (found) return found;
        }
    }
    return null;
}

export function fallOf(list: StepList, ref: ListRef): string | null {
    if (ref === null) return null;
    const owner = ownerOf(list, ref.step);
    if (owner === null) return null;
    const around = listAt(list, owner.ref);
    return around.rows[owner.index + 1]?.name ?? afterEnd(around.last, fallOf(list, owner.ref));
}

export function moveRow(list: StepList, name: string, direction: "up" | "down"): StepList {
    const from = ownerOf(list, name);
    if (from === null) return list;
    const moved = listAt(list, from.ref).rows[from.index];
    if (moved === undefined) return list;
    const rest = changeList(list, from.ref, (one) => ({ ...one, rows: one.rows.filter((row) => row.name !== name) }));
    const insert = (ref: ListRef, at: number | "end") =>
        changeList(rest, ref, (one) => {
            const index = at === "end" ? one.rows.length : at;
            return { ...one, rows: [...one.rows.slice(0, index), moved, ...one.rows.slice(index)] };
        });
    const siblings = listAt(rest, from.ref).rows;
    if (direction === "up") {
        const before = siblings[from.index - 1];
        if (before) {
            const arm = armsOf(before).at(-1);
            return arm ? insert({ step: before.name, arm: arm.arm }, "end") : insert(from.ref, from.index - 1);
        }
    } else {
        const after = siblings[from.index];
        if (after) {
            const arm = armsOf(after)[0];
            return arm ? insert({ step: after.name, arm: arm.arm }, 0) : insert(from.ref, from.index + 1);
        }
    }
    if (from.ref === null) return list;
    const ownerName = from.ref.step;
    const arm = from.ref.arm;
    const owner = ownerOf(rest, ownerName);
    const ownerRow = findRow(rest, ownerName);
    if (owner === null || ownerRow === undefined) return list;
    const arms = armsOf(ownerRow);
    const at = arms.findIndex((other) => other.arm === arm);
    const neighbour = arms[direction === "up" ? at - 1 : at + 1];
    if (neighbour) return insert({ step: ownerName, arm: neighbour.arm }, direction === "up" ? "end" : 0);
    return insert(owner.ref, direction === "up" ? owner.index : owner.index + 1);
}

function ifNeighbour(list: StepList, name: string, direction: "up" | "down"): StepRow | undefined {
    const from = ownerOf(list, name);
    if (from === null) return undefined;
    const rows = listAt(list, from.ref).rows;
    return rows[from.index + (direction === "up" ? -1 : 1)];
}

export function canMoveIf(list: StepList, name: string, direction: "up" | "down"): boolean {
    const neighbour = ifNeighbour(list, name, direction);
    if (neighbour === undefined || neighbour.branches !== undefined) return false;
    const { success } = neighbour;
    return success === undefined || (success.rows.length === 0 && (success.last === null || success.last === "end"));
}

export function moveIf(list: StepList, name: string, direction: "up" | "down"): StepList {
    const moving = findRow(list, name);
    const neighbour = ifNeighbour(list, name, direction);
    if (moving?.branches === undefined || neighbour === undefined || !canMoveIf(list, name, direction)) return list;
    const { branches, ...left } = moving;
    const { success: _, ...joined } = neighbour;
    return changeRow(
        changeRow(list, name, () => (left.failure || left.unavailable ? { ...left, success: emptyList() } : left)),
        neighbour.name,
        () => ({ ...joined, branches }),
    );
}

export function canSwapBranch(row: StepRow, index: number, direction: "up" | "down"): boolean {
    const other = row.branches?.[index + (direction === "up" ? -1 : 1)];
    return row.branches?.[index]?.when !== undefined && other?.when !== undefined;
}

export function swapBranch(list: StepList, name: string, index: number, direction: "up" | "down"): StepList {
    return changeRow(list, name, (row) => {
        const at = index + (direction === "up" ? -1 : 1);
        const branches = [...(row.branches ?? [])];
        const one = branches[index];
        const other = branches[at];
        if (one === undefined || other === undefined || !canSwapBranch(row, index, direction)) return row;
        branches[index] = other;
        branches[at] = one;
        return { ...row, branches };
    });
}

export function withoutRow(list: StepList, name: string): StepList {
    const from = ownerOf(list, name);
    return from === null ? list : changeList(list, from.ref, (one) => ({ ...one, rows: one.rows.filter((row) => row.name !== name) }));
}

export function renamedList(list: StepList, from: string, to: string): StepList {
    const again = (one: StepList): StepList => ({
        ...one,
        last: one.last !== null && one.last !== "end" && one.last.next === from ? { next: to } : one.last,
        rows: one.rows.map((row) => {
            const named: StepRow = row.name === from ? { ...row, name: to } : row;
            return armsOf(named).reduce((done, arm) => withArm(done, arm.arm, again(arm.list)), named);
        }),
    });
    return again(list);
}
