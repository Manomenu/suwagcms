import type { ReactNode } from "react";
import { Link } from "wouter";

import { Button } from "@/design/button";

import type { KnowledgeBase } from "./api";

/** The Knowledge bases screen: the heading, the list with its actions, and whatever form is open. */
export function KnowledgeBasesView({
    bot,
    bases,
    form,
    onAdd,
    onRename,
    onDelete,
}: {
    bot: string;
    bases: KnowledgeBase[];
    form: ReactNode;
    onAdd: () => void;
    onRename: (base: KnowledgeBase) => void;
    onDelete: (base: KnowledgeBase) => void;
}) {
    return (
        <main className="mx-auto flex max-w-2xl flex-col gap-4 p-4">
            <header className="flex items-center gap-2">
                <h1 className="flex-1 text-2xl font-semibold">Knowledge bases of {bot}</h1>
                <Link href={`~/bots/${encodeURIComponent(bot)}`} className="text-sm text-primary underline underline-offset-4">
                    Bots
                </Link>
                <Button onClick={onAdd}>New knowledge base</Button>
            </header>
            {form}
            {bases.length === 0 ? (
                <p className="text-muted-foreground">
                    No knowledge bases yet. Create the first one: it groups the articles of the bot, for people to find and work on them
                    apart.
                </p>
            ) : (
                <ul className="flex flex-col gap-2">
                    {bases.map((base) => (
                        <li key={base.id} className="flex items-center gap-3 rounded-lg border p-3">
                            <div className="flex flex-1 flex-col gap-1">
                                <span className="font-medium">{base.title}</span>
                                <span className="text-sm text-muted-foreground">{base.id}</span>
                            </div>
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                    onRename(base);
                                }}
                            >
                                Rename
                            </Button>
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                    onDelete(base);
                                }}
                            >
                                Delete
                            </Button>
                        </li>
                    ))}
                </ul>
            )}
        </main>
    );
}
