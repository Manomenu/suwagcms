import { useEffect, useState } from "react";
import { useLocation } from "wouter";

import { Alert } from "@/design/alert";

import { fetchBots, type Bot } from "./api";
import { BotsView, profilePath } from "./BotsView";
import { RegisterBotForm } from "./RegisterBotForm";

/**
 * Loads the bots when it opens, so a bot registered meanwhile from the CLI or another tab is found. A new `key` (another
 * bot in the address) opens it afresh.
 */
export function BotsScreen({ openId }: { openId: string | null }) {
    const [bots, setBots] = useState<Bot[] | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [registering, setRegistering] = useState(false);
    const [, navigate] = useLocation();

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        fetchBots(controller.signal)
            .then(setBots)
            .catch((error: unknown) => {
                if (!controller.signal.aborted) setFailure(error instanceof Error ? error.message : String(error));
            });
        return () => {
            controller.abort();
        };
    }, []);

    if (failure !== null) {
        return (
            <main className="mx-auto max-w-2xl p-4">
                <Alert variant="destructive">{failure}</Alert>
            </main>
        );
    }
    if (bots === null) return <main className="mx-auto max-w-2xl p-4 text-muted-foreground">Loading bots…</main>;

    return (
        <BotsView
            bots={bots}
            openId={openId}
            onRegister={() => {
                setRegistering(true);
            }}
            onSwitch={(id) => {
                navigate(`~${profilePath(id)}`);
            }}
            registerForm={
                registering ? (
                    <RegisterBotForm
                        onRegistered={(bot) => {
                            navigate(`~${profilePath(bot.id)}`);
                        }}
                    />
                ) : null
            }
        />
    );
}
