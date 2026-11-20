import { describe, expect, it } from "vitest";

const sources = import.meta.glob<string>(
    ["/src/**/*.ts", "/src/**/*.tsx", "!/src/**/*.test.*", "!/src/**/*.e2e.ts", "!/src/api/openapi.d.ts"],
    {
        query: "?raw",
        import: "default",
        eager: true,
    },
);

function filesWith(pattern: RegExp, except: (path: string) => boolean = () => false): string[] {
    return Object.entries(sources)
        .filter(([path, source]) => !except(path) && pattern.test(source))
        .map(([path]) => path);
}

const inDesign = (path: string) => path.startsWith("/src/design/");
const RESOURCE_FEATURE = /^\/src\/(facts|knowledge-bases|articles|procedures|actions)\//;

describe("the rules of every screen", () => {
    it("the source is really read", () => {
        expect(Object.keys(sources)).toContain("/src/design/field.tsx");
        expect(filesWith(/export function Field/)).toEqual(["/src/design/field.tsx"]);
    });

    it("AC-web-shell-12: no form writes its own legend, Label or ⓘ: Field and Legend from design/field require the help", () => {
        expect(filesWith(/<legend|<Label\b|ⓘ/, inDesign)).toEqual([]);
    });

    it("AC-web-shell-12: a raw label only wraps a checkbox, a radio or a row beside its InfoTip or under a Legend", () => {
        expect(filesWith(/<label\b/, inDesign).sort()).toEqual([
            "/src/procedures/Checklist.tsx",
            "/src/procedures/RuleEditor.tsx",
            "/src/procedures/StepForm.tsx",
            "/src/versions/VersionView.tsx",
            "/src/versions/VersionsView.tsx",
        ]);
    });

    it("AC-web-shell-14: no resource has its own table or row: ResourceList draws the row and its action", () => {
        const own = (path: string) => !RESOURCE_FEATURE.test(path);
        expect(filesWith(/<tr\b|<td\b|<Table\b|@\/design\/table/, own)).toEqual([]);
    });

    it("AC-web-shell-14: every resource's screen uses the shared list and edit screen", () => {
        for (const feature of [
            "facts/Facts",
            "knowledge-bases/KnowledgeBases",
            "articles/Articles",
            "actions/Actions",
            "procedures/Procedures",
        ]) {
            expect(sources[`/src/${feature}View.tsx`]).toContain("<ResourceList");
        }
        for (const feature of [
            "facts/Facts",
            "knowledge-bases/KnowledgeBases",
            "articles/Articles",
            "actions/Actions",
            "procedures/Procedure",
        ]) {
            expect(sources[`/src/${feature}Screen.tsx`]).toContain("<ResourceEdit");
        }
    });

    it("AC-procedure-creator-23: a procedure's edit page draws no heading, Delete or History of its own: the shared edit screen does", () => {
        const page = /<h1\b|<Page\b|<DeleteAction\b|<HistoryPanel\b|<UsagePanel\b|<ChangeIdDialog\b/;
        expect(filesWith(page, (path) => !path.startsWith("/src/procedures/") || path.endsWith("/ProceduresScreen.tsx"))).toEqual([]);
    });

    it("AC-procedure-creator-49: every row of the steps list is a ListRow: no other procedure source draws a row, its fold toggle, ↑ ↓ or ✕", () => {
        const notProcedures = (path: string) => !path.startsWith("/src/procedures/");
        expect(filesWith(/data-row=|aria-expanded|[↑↓]/, notProcedures)).toEqual(["/src/procedures/ListRow.tsx"]);
        expect(sources["/src/procedures/StepsEditor.tsx"]).not.toMatch(/✕|<button\b|Remove them/);
        expect(sources["/src/procedures/StepsEditor.tsx"]).toContain("<ListRow");
        for (const kind of ["failure", "unavailable", "success"]) {
            expect(sources["/src/procedures/StepsEditor.tsx"]).toMatch(new RegExp(`<ListRow\\s+kind="${kind}"`));
        }
    });

    it("AC-procedure-creator-45: every edit screen's Save sits in the shared actions that stay in view", () => {
        expect(sources["/src/design/formFooter.tsx"]).toContain("<StickyActions");
        expect(sources["/src/procedures/ProcedureForm.tsx"]).toContain("<StickyActions");
    });

    it("AC-pagination-9: every list draws its controls with the one module, and nothing else moves between pages", () => {
        const moves = />\s*(Previous|Next)( page)?\s*<|["'`](Previous|Next)( page)?["'`]/;
        expect(filesWith(moves)).toEqual(["/src/design/pagination.tsx"]);
        expect(filesWith(/older changes|load more|show more/i)).toEqual([]);
        expect(filesWith(/<Paginated\b/).sort()).toEqual([
            "/src/design/changeList.tsx",
            "/src/design/resourceList.tsx",
            "/src/versions/VersionsView.tsx",
        ]);
        expect(filesWith(/usePagedRows\(/).sort()).toEqual([
            "/src/actions/ActionsView.tsx",
            "/src/articles/ArticlesView.tsx",
            "/src/facts/FactsView.tsx",
            "/src/knowledge-bases/KnowledgeBasesView.tsx",
            "/src/procedures/ProceduresView.tsx",
            "/src/versions/VersionsView.tsx",
        ]);
        expect(sources["/src/design/pagination.tsx"]).not.toMatch(/history/i);
        expect(sources["/src/api/pagination.ts"]).not.toMatch(/history/i);
    });

    it("AC-change-history-18: every list of changes uses the one module, and no other source draws a change's row or its lines", () => {
        expect(filesWith(/<ChangeList\b/).sort()).toEqual(["/src/design/page.tsx", "/src/history/HistoryScreen.tsx"]);
        expect(filesWith(/<ChangeRow\b/).sort()).toEqual(["/src/design/changeList.tsx", "/src/versions/comparison.tsx"]);
        expect(sources["/src/procedures/ProcedureScreen.tsx"]).toContain("<ResourceEdit");
        expect(sources["/src/procedures/ProcedureScreen.tsx"]).toContain("history={history}");
        expect(filesWith(/<ComparisonRows\b/).sort()).toEqual(["/src/versions/VersionView.tsx", "/src/versions/VersionsView.tsx"]);
        expect(filesWith(/"[−+] "|changedLines/)).toEqual(["/src/design/changeList.tsx"]);
        expect(filesWith(/\.lines\b/).sort()).toEqual(["/src/design/changeIdDialog.tsx", "/src/design/changeList.tsx"]);
        expect(filesWith(/ComparisonPanel|ComparisonDetails|HistoryList/)).toEqual([]);
    });

    it("AC-change-history-34: every main screen hands the page frame its History panel, which the frame draws last", () => {
        const tabs = [...(sources["/src/shell/navigation.ts"] ?? "").matchAll(/id: "([\w-]+)"/g)].map((match) => match[1]);
        const panelOf: Record<string, string> = {
            facts: "/src/facts/FactsView.tsx",
            "knowledge-bases": "/src/knowledge-bases/KnowledgeBasesView.tsx",
            articles: "/src/articles/ArticlesView.tsx",
            procedures: "/src/procedures/ProceduresView.tsx",
            actions: "/src/actions/ActionsView.tsx",
            persona: "/src/personas/PersonaView.tsx",
            versions: "/src/versions/VersionsView.tsx",
        };
        expect(tabs.filter((tab) => tab !== "history")).toEqual(Object.keys(panelOf));
        for (const [tab, path] of Object.entries(panelOf)) {
            expect(sources[path], `the ${tab} screen gives its frame no history: pass history={history} to Page or ResourceList`).toMatch(
                /<(Page|ResourceList)\b[^>]*\bhistory=\{history\}/,
            );
        }
        expect(sources["/src/design/resourceList.tsx"]).toMatch(/<Page\b[^>]*\bhistory=\{history\}/);
        expect(sources["/src/design/resourceEdit.tsx"]).toContain("{...(editedId === null ? {} : { history })}");
        expect(sources["/src/versions/VersionView.tsx"]).toMatch(/<Page\b[^>]*\bhistory=\{\{/);
        expect(sources["/src/design/page.tsx"]).toMatch(
            /\{children\}\s*\{history !== undefined && <HistoryPanel \{\.\.\.history\} \/>\}\s*<\/div>/,
        );
    });

    it("AC-change-history-35: the History panel is one shared part of the page frame, never a copy", () => {
        expect(filesWith(/<HistoryPanel\b|function HistoryPanel|aria-label="History"/)).toEqual(["/src/design/page.tsx"]);
        expect(sources["/src/design/page.tsx"]).not.toMatch(/export function HistoryPanel/);
        expect(filesWith(/<ChangeList\b/, (path) => path === "/src/history/HistoryScreen.tsx")).toEqual(["/src/design/page.tsx"]);
        expect(filesWith(/(?<!~)\/bots\/[^`]*\/(history|window)`|\["HistoryPage"\]/)).toEqual(["/src/api/history.ts"]);
    });

    it("AC-change-history-37: each screen asks for the history of what it is about", () => {
        expect(sources["/src/api/history.ts"]).toContain('collection === null ? "" : `&kind=${collection}`');
        const editor = sources["/src/api/resourceEditor.ts"];
        expect(editor).toContain('useKindHistory(bot, address.screen === "list" ? api.collection : null)');
        expect(editor).toContain("useResourceHistory(bot, api.collection, id, revision)");
        for (const screen of ["facts/Facts", "knowledge-bases/KnowledgeBases", "articles/Articles", "actions/Actions"]) {
            expect(sources[`/src/${screen}Screen.tsx`]).toContain("history={editor.listHistory}");
        }
        expect(sources["/src/procedures/ProceduresScreen.tsx"]).toContain('useKindHistory(bot, listed ? "procedures" : null)');
        expect(sources["/src/procedures/ProcedureScreen.tsx"]).toContain('useResourceHistory(bot, "procedures", id,');
        expect(sources["/src/versions/VersionsScreen.tsx"]).toContain('useKindHistory(bot, earlier === null ? "versions" : null');
        expect(sources["/src/versions/VersionScreen.tsx"]).toContain("useVersionWindow(bot, number)");
        expect(sources["/src/personas/PersonaScreen.tsx"]).toContain('useResourceHistory(bot, "persona", PERSONA_ID');
    });

    it("AC-change-history-19: the shared module holds no word for what it lists", () => {
        expect(sources["/src/design/changeList.tsx"]).not.toMatch(/history/i);
    });

    it("AC-pagination-12: the panel asks for as many entries as the server's page size, never a number of its own", () => {
        expect(filesWith(/[?&]limit=\d/)).toEqual([]);
        expect(sources["/src/api/history.ts"]).toContain("?limit=${size}&page=${page}");
    });

    it("AC-delete-guard-20: Delete has one place: DeleteAction, which only the edit screens use", () => {
        expect(filesWith(/>\s*Delete\s*</)).toEqual(["/src/design/deleteAction.tsx"]);
        expect(filesWith(/<DeleteAction\b/).sort()).toEqual(["/src/design/resourceEdit.tsx"]);
    });

    it("AC-web-shell-7: an address of a resource is built only in api/resourceAddress", () => {
        expect(filesWith(/(navigate\(|href=\{?|href = )[^\n]*\/(procedures|facts|articles|actions|knowledge-bases)\b/)).toEqual([]);
    });

    it("AC-delete-guard-7: a fact, a knowledge base and an action refer to nothing, so their edit screens turn Uses off", () => {
        expect(filesWith(/showUses=\{false\}/, (path) => path.endsWith(".test.tsx")).sort()).toEqual([
            "/src/actions/ActionsScreen.tsx",
            "/src/facts/FactsScreen.tsx",
            "/src/knowledge-bases/KnowledgeBasesScreen.tsx",
        ]);
    });

    it("AC-web-shell-16: nothing uses browser storage: what says where a person is goes in the address", () => {
        const self = "localStorage|sessionStorage";
        expect(filesWith(new RegExp(self))).toEqual([]);
    });
});
