import type { Mistake } from "@/api/client";
import { cn } from "@/design/classNames";

import { consequenceOf, type Check } from "./mistakes";

/** The live list: what the server says the drawing would produce. Clicking a mistake selects its step or the panel. */
export function MistakeList({ check, onPick }: { check: Check; onPick: (mistake: Mistake) => void }) {
    const mistakes = check.state === "done" ? check.mistakes : [];
    return (
        <section aria-label="Mistakes" className="flex flex-col gap-2">
            <h2 className="text-base font-semibold">Mistakes{check.state === "done" ? ` (${mistakes.length})` : ""}</h2>
            {check.state === "checking" && <p className="text-sm text-muted-foreground">Checking…</p>}
            {check.state === "unreachable" && (
                <p role="status" className="rounded-md border border-destructive p-2 text-sm text-destructive">
                    Could not check: {check.message}
                </p>
            )}
            {check.state === "done" && mistakes.length === 0 && <p className="text-sm text-success">No mistakes: ready for a version.</p>}
            <ul className="flex flex-col gap-2">
                {mistakes.map((mistake) => (
                    <li key={`${mistake.code}:${mistake.path}`}>
                        <button
                            type="button"
                            className={cn(
                                "w-full cursor-pointer rounded-md border p-2 text-left text-sm hover:bg-accent",
                                mistake.stage === "Save" ? "border-destructive" : "border-warning",
                            )}
                            onClick={() => {
                                onPick(mistake);
                            }}
                        >
                            <span className="block text-xs font-medium text-muted-foreground uppercase">{consequenceOf(mistake)}</span>
                            <span className="block">{mistake.message}</span>
                            <span className="block text-muted-foreground">{mistake.fix}</span>
                        </button>
                    </li>
                ))}
            </ul>
        </section>
    );
}
