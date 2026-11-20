import { Alert } from "@maniumek/suwfrontkit";
import { Redirect } from "wouter";

import { useResourceEditor } from "@/api/resourceEditor";
import { ResourceEdit } from "@/design/resourceEdit";

import { factsApi } from "./api";
import { emptyForm, formOf } from "./factForm";
import { FactForm } from "./FactForm";
import { FactsView } from "./FactsView";

export function FactsScreen({ bot }: { bot: string }) {
    const editor = useResourceEditor({ bot, api: factsApi, idOf: (fact) => fact.name, usage: "facts" });

    if (editor.redirect !== null) return <Redirect to={editor.redirect} replace />;
    if (editor.failure !== null) return <Alert variant="destructive">{editor.failure}</Alert>;
    if (editor.items === null) return <p className="text-muted-foreground">Loading facts…</p>;
    if (!editor.isOpen)
        return <FactsView bot={bot} history={editor.listHistory} facts={editor.items} onAdd={editor.add} onEdit={editor.edit} />;

    return (
        <ResourceEdit showUses={false} noun="fact" plural="facts" addTitle="Add a fact" idWord="name" {...editor.panels}>
            <FactForm
                key={editor.item?.name ?? "add"}
                bot={bot}
                initial={editor.item === null ? emptyForm() : formOf(editor.item)}
                {...editor.formProps}
            />
        </ResourceEdit>
    );
}
