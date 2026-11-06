// A person reads what changed: the bot's page lists every change newest first, and a deleted resource's history stays readable.
import { expect, test, type APIRequestContext } from "@playwright/test";
import { testBots } from "../../e2e/bots";

const { newBot } = testBots("hist");

test("AC-change-history-6: the bot's History page lists every change newest first, and AC-change-history-4: a deleted resource's history stays readable", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const base = `/api/bots/${bot}/knowledge-bases/oferta`;
    expect((await request.put(`${base}?ifRevision=0`, { data: { title: "Oferta" } })).status()).toBe(200);
    expect((await request.put(`${base}?ifRevision=1`, { data: { title: "Nowa oferta" } })).status()).toBe(200);
    expect((await request.delete(`${base}?ifRevision=2`)).status()).toBe(204);

    await page.goto(`/bots/${bot}`);
    await page.getByRole("navigation").getByRole("link", { name: "History", exact: true }).click();
    await expect(page).toHaveURL(`/bots/${bot}/history`);
    const actions = page.locator("li [data-slot=badge]");
    await expect(actions).toHaveText(["deleted", "edited", "added"], { ignoreCase: true });

    await page.getByRole("link", { name: "oferta", exact: true }).first().click();
    await expect(page.getByRole("heading", { name: "History of oferta" })).toBeVisible();
    await expect(page.getByText("− Nowa oferta").first()).toBeVisible();
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
    expect((await request.put(`${base}?ifRevision=0`, { data: { title: "Oferta" } })).status()).toBe(200);
    expect((await request.put(`${base}?ifRevision=1`, { data: { title: "Nowa oferta" } })).status()).toBe(200);
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await page.getByRole("row").filter({ hasText: "oferta" }).getByRole("button", { name: "Edit" }).click();

    const panel = page.getByRole("region", { name: "History" });
    await expect(panel.getByText("revision 2")).toBeVisible();
    await expect(panel.getByText("− Oferta")).toBeVisible();
    await expect(panel.getByText("+ Nowa oferta")).toBeVisible();
});
