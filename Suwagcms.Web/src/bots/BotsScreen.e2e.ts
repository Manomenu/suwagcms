// A person registers a bot in the browser and works on it; the bot is in the address, so two tabs never share it.
import { expect, test, type APIRequestContext } from "@playwright/test";

function freshId(prefix: string): string {
    return `${prefix}-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

async function register(request: APIRequestContext, id: string): Promise<void> {
    const response = await request.post("/api/bots", {
        data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` },
    });
    expect(response.status()).toBe(201);
}

test("AC-bot-profiles-2: registering a bot opens its profile at /bots/<id>, and AC-bot-profiles-1: it is in the list", async ({ page }) => {
    const id = freshId("web");
    await page.goto("/bots");

    await page.getByRole("button", { name: "Register a bot" }).click();
    await page.getByLabel("Id").fill(id);
    await page.getByLabel("Name", { exact: true }).fill("Registered in the web");
    await page.getByLabel("Address").fill("http://registered.internal:8080");
    await page.getByRole("button", { name: "Register", exact: true }).click();

    await expect(page).toHaveURL(`/bots/${id}`);
    await expect(page.getByRole("navigation").getByRole("listitem").filter({ hasText: "Registered in the web" })).toHaveAttribute(
        "aria-current",
        "page",
    );
    await page.goto("/bots");
    await expect(page.getByRole("main").getByRole("link", { name: "Registered in the web" })).toBeVisible();
    await expect(page.getByRole("main").getByText("http://registered.internal:8080")).toBeVisible();
});

test("AC-bot-profiles-4: a refused registration shows every mistake with its fix and registers nothing", async ({ page }) => {
    await page.goto("/bots");
    await page.getByRole("button", { name: "Register a bot" }).click();
    await page.getByLabel("Address").fill("not an address");

    await page.getByRole("button", { name: "Register", exact: true }).click();

    const alert = page.getByRole("alert");
    await expect(alert).toContainText("The id is missing or not valid");
    await expect(alert).toContainText("The name is blank.");
    await expect(alert).toContainText("Send the address where the CMS reaches the bot");
    await expect(page).toHaveURL("/bots");
});

test("AC-bot-profiles-8: switching the profile in one tab changes neither another tab's bot nor anything on the server", async ({
    context,
    request,
}) => {
    const first = freshId("tab-a");
    const second = freshId("tab-b");
    await register(request, first);
    await register(request, second);
    const tabOne = await context.newPage();
    const tabTwo = await context.newPage();
    await tabOne.goto(`/bots/${first}`);
    await tabTwo.goto(`/bots/${first}`);
    const changes: string[] = [];
    tabOne.on("request", (call) => {
        if (call.url().includes("/api/") && call.method() !== "GET") changes.push(`${call.method()} ${call.url()}`);
    });

    await tabOne
        .getByRole("navigation")
        .getByRole("link", { name: `Name of ${second}` })
        .click();

    await expect(tabOne).toHaveURL(`/bots/${second}`);
    await tabTwo.reload();
    await expect(tabTwo).toHaveURL(`/bots/${first}`);
    await expect(
        tabTwo
            .getByRole("navigation")
            .getByRole("listitem")
            .filter({ hasText: `Name of ${first}` }),
    ).toHaveAttribute("aria-current", "page");
    expect(changes).toEqual([]);
});

test("AC-bot-profiles-11: an address naming a bot that does not exist shows a page saying so, with a link to the list", async ({
    page,
}) => {
    await page.goto("/bots/no-such-bot");

    await expect(page.getByText("No bot 'no-such-bot'")).toBeVisible();
    await page.getByRole("main").getByRole("link", { name: "Bots", exact: true }).click();
    await expect(page).toHaveURL("/bots");
});
