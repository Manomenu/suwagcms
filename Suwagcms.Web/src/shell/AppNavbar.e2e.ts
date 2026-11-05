// Why: the panel is how a person reaches every screen, so the bot-first rule is proven in a real browser.
import { expect, test } from "@playwright/test";

const TABS = [
    ["Facts", "facts"],
    ["Knowledge bases", "knowledge-bases"],
    ["Articles", "articles"],
    ["Procedures", "procedures"],
    ["Versions", "versions"],
    ["History", "history"],
] as const;

function freshId(prefix: string): string {
    return `${prefix}-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

test("AC-web-shell-1, AC-web-shell-2, AC-web-shell-3: the panel lists the bots with no tabs until one is opened, then follows each tab", async ({
    page,
    request,
}) => {
    const id = freshId("panel");
    const response = await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` } });
    expect(response.status()).toBe(201);
    await page.goto("/");
    await expect(page).toHaveURL("/bots");
    const panel = page.getByRole("navigation");
    await expect(panel.getByRole("link", { name: `Name of ${id}` })).toBeVisible();
    for (const [label] of TABS) await expect(panel.getByRole("link", { name: label, exact: true })).toHaveCount(0);

    await panel.getByRole("link", { name: `Name of ${id}` }).click();

    await expect(page).toHaveURL(`/bots/${id}`);
    for (const [label, slug] of TABS) {
        await panel.getByRole("link", { name: label, exact: true }).click();
        await expect(page).toHaveURL(new RegExp(`/bots/${id}/${slug}`));
        await expect(panel.getByRole("link", { name: label, exact: true })).toHaveAttribute("aria-current", "page");
    }
});
