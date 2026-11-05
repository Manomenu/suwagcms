function linesOf(value: unknown): string[] {
    if (value === null || value === undefined) return [];
    return (typeof value === "string" ? value : JSON.stringify(value)).split("\n");
}

/** What a field lost and gained, line by line: a long text changes in a line or two, so the lines both sides share at either end are left out. */
export function changedLines(old: unknown, fresh: unknown): { removed: string[]; added: string[] } {
    const was = linesOf(old);
    const now = linesOf(fresh);
    let start = 0;
    while (start < was.length && start < now.length && was[start] === now[start]) start++;
    let end = 0;
    while (end < was.length - start && end < now.length - start && was[was.length - 1 - end] === now[now.length - 1 - end]) end++;
    return { removed: was.slice(start, was.length - end), added: now.slice(start, now.length - end) };
}
