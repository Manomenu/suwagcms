import { useState } from "react";

import type { Bot } from "@/api/bots";
import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { Input } from "@/design/input";
import { Label } from "@/design/label";

import { registerBot } from "./api";

/** The form that registers a bot; a refusal shows every mistake with its fix, and nothing is registered. */
export function RegisterBotForm({ onRegistered, onCancel }: { onRegistered: (bot: Bot) => void; onCancel: () => void }) {
    const [id, setId] = useState("");
    const [name, setName] = useState("");
    const [address, setAddress] = useState("");
    const [refusal, setRefusal] = useState<ApiError | null>(null);

    function submit(event: React.SyntheticEvent) {
        event.preventDefault();
        setRefusal(null);
        registerBot({ id, name, address })
            .then(onRegistered)
            .catch((error: unknown) => {
                setRefusal(error instanceof ApiError ? error : new ApiError(String(error)));
            });
    }

    return (
        <form onSubmit={submit} className="flex max-w-2xl flex-col gap-3">
            <div className="flex flex-col gap-2">
                <Label htmlFor="bot-id">Id</Label>
                <Input
                    id="bot-id"
                    value={id}
                    placeholder="rzeczoznawca"
                    onChange={(event) => {
                        setId(event.currentTarget.value);
                    }}
                />
            </div>
            <div className="flex flex-col gap-2">
                <Label htmlFor="bot-name">Name</Label>
                <Input
                    id="bot-name"
                    value={name}
                    placeholder="Rzeczoznawca"
                    onChange={(event) => {
                        setName(event.currentTarget.value);
                    }}
                />
            </div>
            <div className="flex flex-col gap-2">
                <Label htmlFor="bot-address">Address</Label>
                <Input
                    id="bot-address"
                    value={address}
                    placeholder="http://rzeczoznawca.internal:8080"
                    onChange={(event) => {
                        setAddress(event.currentTarget.value);
                    }}
                />
            </div>
            {refusal !== null && (
                <Alert variant="destructive">
                    {refusal.mistakes.length === 0 ? (
                        refusal.message
                    ) : (
                        <ul className="list-disc pl-4">
                            {refusal.mistakes.map((mistake) => (
                                <li key={`${mistake.code}:${mistake.path}`}>
                                    {mistake.message} {mistake.fix}
                                </li>
                            ))}
                        </ul>
                    )}
                </Alert>
            )}
            <div className="flex gap-2">
                <Button type="submit">Register</Button>
                <Button type="button" variant="outline" onClick={onCancel}>
                    Cancel
                </Button>
            </div>
        </form>
    );
}
