import { Alert, Spinner } from "@maniumek/suwfrontkit";
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
    if (editor.items === null) return <Spinner label="Loading knowledge bases…" size="sm" />;
    if (!editor.isOpen)
        return (
            <KnowledgeBasesView
                bot={bot}
                history={editor.listHistory}
                bases={editor.items}
                addHref={editor.addHref}
                hrefOf={editor.hrefOf}
            />
        );

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
