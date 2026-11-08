// A person sees, at the bottom of each main screen, the changes of what that screen is about and nothing else.
import { expect, test, type APIRequestContext } from "@playwright/test";
import { testBots } from "../../e2e/bots";

const { newBot } = testBots("panels");

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

test("AC-change-history-37: a procedure added shows in the Procedures screen's panel and not in the Articles screen's (AC-change-history-34)", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addConversation(request, bot);

    await page.goto(`/bots/${bot}/procedures`);
    const panel = page.getByRole("region", { name: "History" });
    const added = panel.locator("li").filter({ hasText: "conversation" });
    await expect(added.locator("[data-slot=badge]")).toHaveText("added", { ignoreCase: true });
    await expect(added.getByRole("link", { name: "conversation" })).toBeVisible();

    await page.getByRole("navigation").getByRole("link", { name: "Articles", exact: true }).click();
    await expect(page).toHaveURL(`/bots/${bot}/articles`);
    await expect(panel.getByText(/No change has been recorded since \d{4}-\d{2}-\d{2}/)).toBeVisible();
    await expect(panel).not.toContainText("conversation");
});

test("AC-change-history-37: the Versions panel shows a created and a pushed version, and a version created in the browser joins it", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addConversation(request, bot);
    expect((await request.post(`/api/bots/${bot}/versions`)).status()).toBe(201);
    // Why: the test bot's address answers nothing, so the push fails, and a failed push is a version event all the same.
    expect((await request.post(`/api/bots/${bot}/versions/1/push`)).status()).toBe(200);

    await page.goto(`/bots/${bot}/versions`);
    const panel = page.getByRole("region", { name: "History" });
    await expect(panel.locator("li [data-slot=badge]")).toHaveText(["pushed", "created"], { ignoreCase: true });
    await expect(panel).not.toContainText("conversation");

    // Why: a version is refused while nothing has changed since the last one.
    const fact = { type: "text", description: "The city", maxLength: 80 };
    expect((await request.put(`/api/bots/${bot}/facts/customer.city?ifRevision=0`, { data: fact })).status()).toBe(200);
    await page.getByRole("button", { name: "Create version" }).click();
    await expect(panel.locator("li [data-slot=badge]")).toHaveText(["created", "pushed", "created"], { ignoreCase: true });
});
