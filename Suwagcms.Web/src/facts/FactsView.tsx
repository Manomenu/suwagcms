import type { ReactNode } from "react";
import { Link } from "wouter";

import { Button } from "@/design/button";

import type { Fact } from "./api";
import { constraintOf } from "./factForm";

/** The Facts screen: the heading, the list with its actions, and whatever form is open. */
export function FactsView({
    bot,
    facts,
    form,
    onAdd,
    onEdit,
}: {
    bot: string;
    facts: Fact[];
    form: ReactNode;
    onAdd: () => void;
    onEdit: (fact: Fact) => void;
}) {
    return (
        <main className="mx-auto flex max-w-2xl flex-col gap-4 p-4">
            <header className="flex items-center gap-2">
                <h1 className="flex-1 text-2xl font-semibold">Facts of {bot}</h1>
                <Link href={`~/bots/${encodeURIComponent(bot)}`} className="text-sm text-primary underline underline-offset-4">
                    Bots
                </Link>
                <Button onClick={onAdd}>Add a fact</Button>
            </header>
            {form}
            {facts.length === 0 ? (
                <p className="text-muted-foreground">
                    No facts yet. Add one: the model reads each fact's description to understand a message.
                </p>
            ) : (
                <ul className="flex flex-col gap-2">
                    {facts.map((fact) => (
                        <li key={fact.name} className="flex items-center gap-3 rounded-lg border p-3">
                            <div className="flex flex-1 flex-col gap-1">
                                <span className="font-medium">{fact.name}</span>
                                <span className="text-sm text-muted-foreground">
                                    {constraintOf(fact)} · {fact.writableBy}
                                </span>
                                <span className="text-sm">{fact.description}</span>
                                {fact.usedBy.length > 0 && (
                                    <span className="text-sm text-muted-foreground">Used by: {fact.usedBy.join(", ")}</span>
                                )}
                            </div>
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                    onEdit(fact);
                                }}
                            >
                                Edit
                            </Button>
                        </li>
                    ))}
                </ul>
            )}
        </main>
    );
}
