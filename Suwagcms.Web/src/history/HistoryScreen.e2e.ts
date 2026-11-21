import { expect, test } from "@playwright/test";
import { testBots } from "../../e2e/bots";

const { newBot } = testBots("hist");

test("AC-change-history-6: the bot's History page lists every change newest first, and AC-change-history-4: a deleted resource's history stays readable", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const base = `/api/bots/${bot}/knowledge-bases/oferta`;
    expect((await request.put(`${base}?ifRevision=0`, { data: { title: "Oferta", embedding: "default", results: 5 } })).status()).toBe(200);
    expect((await request.put(`${base}?ifRevision=1`, { data: { title: "Nowa oferta", embedding: "default", results: 5 } })).status()).toBe(
        200,
    );
    expect((await request.delete(`${base}?ifRevision=2`)).status()).toBe(204);

    await page.goto(`/bots/${bot}`);
    await page.getByRole("navigation").getByRole("link", { name: "History", exact: true }).click();
    await expect(page).toHaveURL(`/bots/${bot}/history`);
    const actions = page.locator("li [data-slot=badge]");
    await expect(actions).toHaveText(["deleted", "edited", "added"], { ignoreCase: true });

    await page.getByRole("link", { name: "oferta", exact: true }).first().click();
    await expect(page.getByRole("heading", { name: "History of oferta" })).toBeVisible();
    await expect(page.locator("li").filter({ hasText: "deleted" })).toContainText("title: Nowa oferta → (none)");
});

test("AC-change-history-1: a resource's History panel shows a change with its old and new values, and one with none says since when", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const fresh = await request.get(`/api/bots/${bot}/knowledge-bases/nothing/history`);
    expect(((await fresh.json()) as { entries: unknown[] }).entries).toEqual([]);
    await page.goto(`/bots/${bot}/history?kind=articles&id=nothing`);
    await expect(page.getByText(/No change has been recorded since \d{4}-\d{2}-\d{2}/)).toBeVisible();

    const base = `/api/bots/${bot}/knowledge-bases/oferta`;
    expect((await request.put(`${base}?ifRevision=0`, { data: { title: "Oferta", embedding: "default", results: 5 } })).status()).toBe(200);
    expect((await request.put(`${base}?ifRevision=1`, { data: { title: "Nowa oferta", embedding: "default", results: 5 } })).status()).toBe(
        200,
    );
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await page.getByRole("row").filter({ hasText: "oferta" }).getByRole("link", { name: "Edit" }).click();

    const panel = page.getByRole("region", { name: "History" });
    await expect(panel.getByText("revision 2")).toBeVisible();
    await expect(panel.locator("li").filter({ hasText: "revision 2" })).toContainText("title: Oferta → Nowa oferta");
});

test("AC-change-history-22: a procedure's edit screen lists its changes in the shared panel, a changed step as a part that opens, with no JSON (AC-change-history-18)", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const definition = (say: string) => ({
        description: "The base",
        firstStep: "wait",
        steps: { wait: { listen: {}, next: "answer" }, answer: { say, next: "wait" } },
    });
    const base = `/api/bots/${bot}/procedures/conversation`;
    expect((await request.put(`${base}?ifRevision=0`, { data: definition("Hello") })).status()).toBe(200);
    expect((await request.put(`${base}?ifRevision=1`, { data: definition("Hi there") })).status()).toBe(200);

    await page.goto(`/bots/${bot}/procedures/conversation/edit`);

    const panel = page.getByRole("region", { name: "History" });
    await expect(panel.getByRole("heading", { name: "History" })).toHaveCount(1);
    const edited = panel.locator("li").filter({ hasText: "revision 2" });
    const part = edited.getByText("step 'answer' changed");
    await expect(part).toBeVisible();
    await expect(edited.getByText("say:")).toBeHidden();
    await part.click();
    await expect(edited.getByText("say:")).toBeVisible();
    await expect(edited).toContainText("Hello → Hi there");
    await expect(edited).not.toContainText("{");
});
