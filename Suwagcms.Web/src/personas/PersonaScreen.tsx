import { useCallback, useEffect, useState, useTransition } from "react";

import { ApiError, type Mistake } from "@/api/client";
import { useResourceHistory } from "@/api/history";
import { Alert } from "@/design/alert";
import { useLeaveGuard } from "@/api/leaveGuard";

import { fetchPersona, savePersona, type Persona } from "./api";
import { bodyOf, formOf, isDirty, isEmpty, type FieldName, type PersonaForm } from "./personaForm";
import { PersonaView } from "./PersonaView";

const PERSONA_ID = "persona";

export function PersonaScreen({ bot }: { bot: string }) {
    const [persona, setPersona] = useState<Persona | null>(null);
    const [form, setForm] = useState<PersonaForm | null>(null);
    const [loadFailure, setLoadFailure] = useState<string | null>(null);
    const [refusal, setRefusal] = useState<ApiError | null>(null);
    const [saved, setSaved] = useState(false);
    const [saving, startSaving] = useTransition();
    const revision = persona === null ? 0 : Number(persona.revision);
    const history = useResourceHistory(bot, "persona", PERSONA_ID, revision);
    useLeaveGuard(
        persona !== null && form !== null && isDirty(form, formOf(persona)),
        "The persona has changes that are not saved. Leave without saving them?",
    );

    const show = useCallback((shown: Persona) => {
        setPersona(shown);
        setForm(formOf(shown));
        setRefusal(null);
    }, []);

    const load = useCallback(
        (signal: AbortSignal) => {
            fetchPersona(bot, signal)
                .then(show)
                .catch((error: unknown) => {
                    if (!signal.aborted) setLoadFailure(error instanceof Error ? error.message : String(error));
                });
        },
        [bot, show],
    );

    useEffect(() => {
        const controller = new AbortController();
        load(controller.signal);
        return () => {
            controller.abort();
        };
    }, [load]);

    if (loadFailure !== null) return <Alert variant="destructive">{loadFailure}</Alert>;
    if (persona === null || form === null) return <p className="text-muted-foreground">Loading the persona…</p>;

    function change(name: FieldName, text: string) {
        setSaved(false);
        setForm((was) => (was === null ? was : { ...was, [name]: text }));
    }

    function save(current: PersonaForm) {
        setSaved(false);
        setRefusal(null);
        startSaving(() =>
            savePersona(bot, bodyOf(current), revision)
                .then((answer) => {
                    show(answer);
                    setSaved(true);
                })
                .catch((error: unknown) => {
                    setRefusal(error instanceof ApiError ? error : new ApiError(String(error)));
                }),
        );
    }

    const mistakes: Mistake[] = refusal?.mistakes ?? [];
    return (
        <PersonaView
            bot={bot}
            form={form}
            revision={revision}
            empty={isEmpty(persona)}
            saved={saved}
            saving={saving}
            failure={refusal !== null && mistakes.length === 0 ? refusal.message : null}
            mistakes={mistakes}
            outOfDate={mistakes.some((mistake) => mistake.code === "resource.stale")}
            history={history}
            onChange={change}
            onSave={() => {
                save(form);
            }}
            onReload={() => {
                setSaved(false);
                load(new AbortController().signal);
            }}
        />
    );
}
