import { useState } from "react";

import type { Bot } from "@/api/bots";
import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { MistakeItems } from "@/design/mistakeItems";

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
            <Field
                prefix="bot"
                name="id"
                label="Id"
                hint="What the CMS and the bot call this bot. The bot answers with it, so a wrong address is noticed. It cannot change."
                errors={[]}
            >
                {(props) => (
                    <Input
                        {...props}
                        value={id}
                        placeholder="rzeczoznawca"
                        onChange={(event) => {
                            setId(event.currentTarget.value);
                        }}
                    />
                )}
            </Field>
            <Field prefix="bot" name="name" label="Name" hint="The name people see in the panel." errors={[]}>
                {(props) => (
                    <Input
                        {...props}
                        value={name}
                        placeholder="Rzeczoznawca"
                        onChange={(event) => {
                            setName(event.currentTarget.value);
                        }}
                    />
                )}
            </Field>
            <Field
                prefix="bot"
                name="address"
                label="Address"
                hint="Where the CMS reaches the bot, to ask what it declares and to push versions."
                errors={[]}
            >
                {(props) => (
                    <Input
                        {...props}
                        value={address}
                        placeholder="http://rzeczoznawca.internal:8080"
                        onChange={(event) => {
                            setAddress(event.currentTarget.value);
                        }}
                    />
                )}
            </Field>
            {refusal !== null && (
                <Alert variant="destructive">
                    {refusal.mistakes.length === 0 ? refusal.message : <MistakeItems mistakes={refusal.mistakes} />}
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
