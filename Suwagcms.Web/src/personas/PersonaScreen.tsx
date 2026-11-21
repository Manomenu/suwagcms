import { Alert, Spinner } from "@maniumek/suwfrontkit";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { type Mistake, messageOf, mistakesOf, refusalOf } from "@/api/client";
import { useResourceHistory } from "@/api/history";
import { useLeaveGuard } from "@maniumek/suwfrontkit";

import { fetchPersona, savePersona, type Persona } from "./api";
import { bodyOf, formOf, isDirty, isEmpty, type FieldName, type PersonaForm } from "./personaForm";
import { PersonaView } from "./PersonaView";

const PERSONA_ID = "persona";

interface Editing {
    base: Persona;
    form: PersonaForm;
}

function editingOf(persona: Persona): Editing {
    return { base: persona, form: formOf(persona) };
}

export function PersonaScreen({ bot }: { bot: string }) {
    const key = ["resources", bot, "persona"];
    const queries = useQueryClient();
    const loaded = useQuery({ queryKey: key, queryFn: ({ signal }) => fetchPersona(bot, signal) });
    const [editing, setEditing] = useState<Editing | null>(null);
    const [saved, setSaved] = useState(false);
    const dirty = editing !== null && isDirty(editing.form, formOf(editing.base));
    if (loaded.data !== undefined && (editing === null || (!dirty && editing.base.revision !== loaded.data.revision)))
        setEditing(editingOf(loaded.data));
    const revision = editing === null ? 0 : Number(editing.base.revision);
    const history = useResourceHistory(bot, "persona", PERSONA_ID, revision);
    const save = useMutation({
        mutationFn: (current: PersonaForm) => savePersona(bot, bodyOf(current), revision),
        onSuccess: (answer) => {
            queries.setQueryData(key, answer);
            setEditing(editingOf(answer));
            setSaved(true);
        },
    });
    useLeaveGuard(dirty, "The persona has changes that are not saved. Leave without saving them?");

    if (loaded.error !== null) return <Alert variant="destructive">{messageOf(loaded.error)}</Alert>;
    if (editing === null) return <Spinner label="Loading the persona…" size="sm" />;
    const { base, form } = editing;

    function change(name: FieldName, text: string) {
        setSaved(false);
        setEditing((was) => (was === null ? was : { ...was, form: { ...was.form, [name]: text } }));
    }

    const refusal = save.error === null ? null : refusalOf(save.error);
    const mistakes: Mistake[] = mistakesOf(refusal);
    return (
        <PersonaView
            bot={bot}
            form={form}
            revision={revision}
            empty={isEmpty(base)}
            saved={saved}
            saving={save.isPending}
            failure={refusal !== null && mistakes.length === 0 ? refusal.message : null}
            mistakes={mistakes}
            outOfDate={mistakes.some((mistake) => mistake.code === "resource.stale")}
            history={history}
            onChange={change}
            onSave={() => {
                setSaved(false);
                save.mutate(form);
            }}
            onReload={() => {
                setSaved(false);
                save.reset();
                void loaded.refetch().then((again) => {
                    if (again.data !== undefined) setEditing(editingOf(again.data));
                });
            }}
        />
    );
}
