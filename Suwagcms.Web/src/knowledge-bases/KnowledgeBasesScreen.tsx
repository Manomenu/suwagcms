import { Alert } from "@maniumek/suwfrontkit";
import { Redirect } from "wouter";

import { useResourceEditor } from "@/api/resourceEditor";
import { ResourceEdit } from "@/design/resourceEdit";

import { knowledgeBasesApi } from "./api";
import { emptyForm, formOf } from "./knowledgeBaseForm";
import { KnowledgeBaseForm } from "./KnowledgeBaseForm";
import { KnowledgeBasesView } from "./KnowledgeBasesView";

export function KnowledgeBasesScreen({ bot }: { bot: string }) {
    const editor = useResourceEditor({ bot, api: knowledgeBasesApi, idOf: (base) => base.id, usage: null });

    if (editor.redirect !== null) return <Redirect to={editor.redirect} replace />;
    if (editor.failure !== null) return <Alert variant="destructive">{editor.failure}</Alert>;
    if (editor.items === null) return <p className="text-muted-foreground">Loading knowledge bases…</p>;
    if (!editor.isOpen)
        return <KnowledgeBasesView bot={bot} history={editor.listHistory} bases={editor.items} onAdd={editor.add} onEdit={editor.edit} />;

    return (
        <ResourceEdit showUses={false} noun="knowledge base" plural="knowledge bases" addTitle="Add a knowledge base" {...editor.panels}>
            <KnowledgeBaseForm
                key={editor.item?.id ?? "add"}
                bot={bot}
                initial={editor.item === null ? emptyForm() : formOf(editor.item)}
                {...editor.formProps}
            />
        </ResourceEdit>
    );
}
