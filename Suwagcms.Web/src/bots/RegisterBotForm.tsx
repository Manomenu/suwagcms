import { Alert, ApiError, Button, Field, Input } from "@maniumek/suwfrontkit";
import { mistakesOf, refusalOf } from "@/api/client";
import { useState, useTransition } from "react";

import type { Bot } from "@/api/bots";
import { MistakeItems } from "@/design/mistakeItems";

import { registerBot } from "./api";

export function RegisterBotForm({ onRegistered, onCancel }: { onRegistered: (bot: Bot) => void; onCancel: () => void }) {
    const [id, setId] = useState("");
    const [name, setName] = useState("");
    const [address, setAddress] = useState("");
    const [refusal, setRefusal] = useState<ApiError | null>(null);
    const [registering, startRegistering] = useTransition();

    function submit(event: React.SyntheticEvent) {
        event.preventDefault();
        setRefusal(null);
        startRegistering(() =>
            registerBot({ id, name, address })
                .then(onRegistered)
                .catch((error: unknown) => {
                    setRefusal(refusalOf(error));
                }),
        );
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
                    {mistakesOf(refusal).length === 0 ? refusal.message : <MistakeItems mistakes={mistakesOf(refusal)} />}
                </Alert>
            )}
            <div className="flex gap-2">
                <Button type="submit" disabled={registering}>
                    Register
                </Button>
                <Button type="button" variant="outline" onClick={onCancel}>
                    Cancel
                </Button>
            </div>
        </form>
    );
}
