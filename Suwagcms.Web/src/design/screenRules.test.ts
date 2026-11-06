// Why: these rules are about what the source of every screen does not contain, which no single screen's test can see.
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
        expect(filesWith(/<ChangeList\b/).sort()).toEqual(["/src/design/historyPanel.tsx", "/src/history/HistoryScreen.tsx"]);
        expect(filesWith(/<ChangeRow\b/).sort()).toEqual(["/src/design/changeList.tsx", "/src/versions/comparison.tsx"]);
        expect(filesWith(/<HistoryPanel\b/).sort()).toEqual([
            "/src/design/resourceEdit.tsx",
            "/src/persona/PersonaScreen.tsx",
            "/src/versions/VersionView.tsx",
        ]);
        expect(sources["/src/procedures/ProcedureScreen.tsx"]).toContain("<ResourceEdit");
        expect(sources["/src/procedures/ProcedureScreen.tsx"]).toContain("history={history}");
        expect(filesWith(/<ComparisonRows\b/).sort()).toEqual(["/src/versions/VersionView.tsx", "/src/versions/VersionsView.tsx"]);
        expect(filesWith(/"[−+] "|changedLines/)).toEqual(["/src/design/changeList.tsx"]);
        // Why: a change-id preview lists its own rewritten lines, which are no lines of a change.
        expect(filesWith(/\.lines\b/).sort()).toEqual(["/src/design/changeIdDialog.tsx", "/src/design/changeList.tsx"]);
        expect(filesWith(/ComparisonPanel|ComparisonDetails|HistoryList/)).toEqual([]);
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
