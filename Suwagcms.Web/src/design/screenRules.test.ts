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
            "/src/versions/ComparisonPanel.tsx",
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
        for (const feature of ["facts/Facts", "knowledge-bases/KnowledgeBases", "articles/Articles", "actions/Actions"]) {
            expect(sources[`/src/${feature}Screen.tsx`]).toContain("<ResourceEdit");
        }
    });

    it("AC-delete-guard-20: Delete has one place: DeleteAction, which only the edit screens use", () => {
        expect(filesWith(/>\s*Delete\s*</)).toEqual(["/src/design/deleteAction.tsx"]);
        expect(filesWith(/<DeleteAction\b/).sort()).toEqual(["/src/design/resourceEdit.tsx", "/src/procedures/ProcedurePanel.tsx"]);
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
