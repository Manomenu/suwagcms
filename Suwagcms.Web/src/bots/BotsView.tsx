import type { ReactNode } from "react";
import { Link } from "wouter";

import { Button } from "@/design/button";
import { cn } from "@/design/classNames";

import type { Bot } from "./api";

export function profilePath(id: string): string {
    return `/bots/${encodeURIComponent(id)}`;
}

/** Nothing registered yet: the one action there is. */
function EmptyState({ onRegister }: { onRegister: () => void }) {
    return (
        <div className="flex flex-col items-start gap-3">
            <p className="text-muted-foreground">No bots yet. Register one to manage its knowledge, procedures and facts.</p>
            <Button onClick={onRegister}>Register a bot</Button>
        </div>
    );
}

/** A web address that names no bot: say so, and the way back to the list. */
function MissingBot({ id }: { id: string }) {
    return (
        <div className="flex flex-col items-start gap-3">
            <h2 className="text-lg font-medium">No bot '{id}'</h2>
            <p className="text-muted-foreground">There is no bot with this id. It may be mistyped, or not registered yet.</p>
            <Link href="~/bots" className="text-primary underline underline-offset-4">
                Bots
            </Link>
        </div>
    );
}

/**
 * The bots screen: the bar with the profile switch, and the list. The bot comes from the address (`openId`), never from
 * the server or from storage, so two tabs can show two bots.
 */
export function BotsView({
    bots,
    openId,
    onRegister,
    onSwitch,
    registerForm,
}: {
    bots: Bot[];
    openId: string | null;
    onRegister: () => void;
    onSwitch: (id: string) => void;
    registerForm: ReactNode;
}) {
    const open = bots.find((bot) => bot.id === openId);
    return (
        <main className="mx-auto flex max-w-2xl flex-col gap-4 p-4">
            <header className="flex items-center gap-2">
                <h1 className="flex-1 text-2xl font-semibold">Bots</h1>
                {bots.length > 0 && (
                    <select
                        aria-label="Bot"
                        className="h-9 rounded-md border border-input bg-background px-2 text-sm"
                        value={open?.id ?? ""}
                        onChange={(event) => {
                            onSwitch(event.currentTarget.value);
                        }}
                    >
                        {open === undefined && (
                            <option value="" disabled>
                                Choose a bot
                            </option>
                        )}
                        {bots.map((bot) => (
                            <option key={bot.id} value={bot.id}>
                                {bot.id}
                            </option>
                        ))}
                    </select>
                )}
                {bots.length > 0 && (
                    <Button variant="outline" onClick={onRegister}>
                        Register a bot
                    </Button>
                )}
            </header>
            {registerForm}
            {openId !== null && open === undefined && <MissingBot id={openId} />}
            {bots.length === 0 && openId === null && <EmptyState onRegister={onRegister} />}
            {bots.length > 0 && (open !== undefined || openId === null) && (
                <ul className="flex flex-col gap-2">
                    {bots.map((bot) => (
                        <li
                            key={bot.id}
                            aria-current={bot.id === openId ? "page" : undefined}
                            className={cn("flex items-center gap-3 rounded-lg border p-3", bot.id === openId && "bg-accent")}
                        >
                            <Link href={`~${profilePath(bot.id)}`} className={cn("flex-1", bot.id === openId && "font-semibold")}>
                                {bot.name}
                            </Link>
                            <span className="text-sm text-muted-foreground">{bot.address}</span>
                            <Link href={`~${profilePath(bot.id)}/facts`} className="text-sm text-primary underline underline-offset-4">
                                Facts
                            </Link>
                        </li>
                    ))}
                </ul>
            )}
        </main>
    );
}
