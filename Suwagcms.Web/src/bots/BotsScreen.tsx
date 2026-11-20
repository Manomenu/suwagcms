import { useEffect, useState } from "react";
import { useLocation } from "wouter";

import { fetchBots, type Bot } from "@/api/bots";
import { Alert } from "@/design/alert";

import { BotsView, profilePath } from "./BotsView";
import { DeclarationPanel } from "./DeclarationPanel";
import { RegisterBotForm } from "./RegisterBotForm";

export function BotsScreen({ openId }: { openId: string | null }) {
    const [bots, setBots] = useState<Bot[] | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [registering, setRegistering] = useState(false);
    const [, navigate] = useLocation();

    useEffect(() => {
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
        return <Alert variant="destructive">{failure}</Alert>;
    }
    if (bots === null) return <p className="text-muted-foreground">Loading bots…</p>;

    return (
        <BotsView
            bots={bots}
            openId={openId}
            declarationPanel={openId === null ? null : <DeclarationPanel bot={openId} />}
            onRegister={() => {
                setRegistering(true);
            }}
            registerForm={
                registering ? (
                    <RegisterBotForm
                        onRegistered={(bot) => {
                            navigate(`~${profilePath(bot.id)}`);
                        }}
                        onCancel={() => {
                            setRegistering(false);
                        }}
                    />
                ) : null
            }
        />
    );
}
