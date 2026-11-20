import { Alert } from "@maniumek/suwfrontkit";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useLocation } from "wouter";

import { BOTS, useBots } from "@/api/bots";

import { BotsView, profilePath } from "./BotsView";
import { DeclarationPanel } from "./DeclarationPanel";
import { RegisterBotForm } from "./RegisterBotForm";

export function BotsScreen({ openId }: { openId: string | null }) {
    const { bots, failure } = useBots();
    const [registering, setRegistering] = useState(false);
    const [, navigate] = useLocation();
    const queries = useQueryClient();

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
                            void queries.invalidateQueries({ queryKey: BOTS });
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
