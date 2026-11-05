// A person creates a version of a bot in the browser; the bot is in the address, and a refusal lists every mistake.
import { expect, test, type APIRequestContext } from "@playwright/test";

function freshId(): string {
    return `versions-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

async function newBot(request: APIRequestContext): Promise<string> {
    const id = freshId();
    const response = await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` } });
    expect(response.status()).toBe(201);
    return id;
}

async function addConversation(request: APIRequestContext, bot: string): Promise<void> {
    const response = await request.put(`/api/bots/${bot}/procedures/conversation?ifRevision=0`, {
        data: {
            description: "The base of every conversation",
            firstStep: "wait",
            steps: { wait: { listen: {}, next: "answer" }, answer: { say: "Hello", next: "wait" } },
        },
    });
    expect(response.status()).toBe(200);
}

test("AC-version-create-2: the web lists every mistake of an unfinished bot, links to the screen to mend it, and creates no version", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/versions`);
    await expect(page.getByText("No versions yet")).toBeVisible();

    await page.getByRole("button", { name: "Create version" }).click();

    await expect(page.getByText("Not created, 1 mistake")).toBeVisible();
    await expect(page.getByText(/no 'conversation' procedure/)).toBeVisible();
    await expect(page.getByRole("link", { name: "Open", exact: true })).toHaveAttribute("href", `/bots/${bot}/procedures`);
    await expect(page.getByText("No versions yet")).toBeVisible();
});

test("AC-version-create-1: Create version in the web makes the next version and shows it in the list, which can be opened (AC-version-create-13)", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addConversation(request, bot);
    await page.goto(`/bots/${bot}/versions`);

    await page.getByRole("button", { name: "Create version" }).click();

    const row = page.getByRole("listitem").filter({ hasText: "v1 ·" });
    await expect(row).toContainText("0 facts, 0 knowledge bases, 0 articles, 1 procedures");
    await row.getByRole("button", { name: "Open" }).click();
    await expect(page.getByText(`"bot":"${bot}"`)).toBeVisible();
    const listed = await request.get(`/api/bots/${bot}/versions`);
    expect(await listed.json()).toMatchObject([{ number: 1, procedures: 1 }]);

    await page.getByRole("button", { name: "Create version" }).click();
    await expect(page.getByText(/Nothing has changed since version 1/)).toBeVisible();
});
