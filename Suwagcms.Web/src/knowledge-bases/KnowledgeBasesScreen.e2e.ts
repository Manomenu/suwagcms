// A person works on the knowledge bases of one bot in the browser; the bot is in the address, and a refusal keeps what was typed.
import { expect, test, type APIRequestContext } from "@playwright/test";
import { testBots } from "../../e2e/bots";

const { newBot } = testBots("kb");

async function addBase(request: APIRequestContext, bot: string, id: string, title: string): Promise<void> {
    const response = await request.put(`/api/bots/${bot}/knowledge-bases/${id}?ifRevision=0`, { data: { title } });
    expect(response.status()).toBe(200);
}

test("AC-knowledge-bases-1: a bot with no bases offers to create the first one, and the base created is listed and in the API", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await expect(page.getByText("No knowledge bases yet.")).toBeVisible();

    await page.getByRole("button", { name: "Add a knowledge base" }).click();
    await page.getByLabel("Id").fill("oferta");
    await page.getByLabel("Title").fill("Offer");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    const row = page.getByRole("row").filter({ hasText: "oferta" });
    await expect(row).toContainText("Offer");
    expect(await (await request.get(`/api/bots/${bot}/knowledge-bases`)).json()).toMatchObject([{ id: "oferta", title: "Offer" }]);
});

test("AC-knowledge-bases-2: a base edited in the web keeps its id", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Offer");
    await page.goto(`/bots/${bot}/knowledge-bases`);

    await page.getByRole("row").filter({ hasText: "oferta" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel("Title").fill("Our offer");
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("row").filter({ hasText: "oferta" })).toContainText("Our offer");
    expect(await (await request.get(`/api/bots/${bot}/knowledge-bases`)).json()).toMatchObject([{ id: "oferta", title: "Our offer" }]);
});

test("AC-knowledge-bases-4: a title another base has is refused beside the title and keeps what was typed", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Our offer");
    await page.goto(`/bots/${bot}/knowledge-bases`);

    await page.getByRole("button", { name: "Add a knowledge base" }).click();
    await page.getByLabel("Id").fill("second");
    await page.getByLabel("Title").fill("  our OFFER ");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    await expect(page.locator('[data-field="title"]')).toContainText("used by knowledge base 'oferta'");
    await expect(page.getByLabel("Id")).toHaveValue("second");
    await expect(page.getByLabel("Title")).toHaveValue("  our OFFER ");
});

test("AC-knowledge-bases-6: editing a base that changed meanwhile is refused with the current revision and keeps what was typed", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "First");
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await page.getByRole("row").filter({ hasText: "oferta" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel("Title").fill("Marta's title");
    const other = await request.put(`/api/bots/${bot}/knowledge-bases/oferta?ifRevision=1`, { data: { title: "The agent's title" } });
    expect(other.status()).toBe(200);

    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("alert")).toContainText("it is now revision 2");
    await expect(page.getByLabel("Title")).toHaveValue("Marta's title");
});

test("AC-knowledge-bases-3: a base deleted in the web is gone from the list and from the API", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Offer");
    await page.goto(`/bots/${bot}/knowledge-bases`);

    await page.getByRole("row").filter({ hasText: "oferta" }).getByRole("button", { name: "Delete" }).click();

    await expect(page.getByText("No knowledge bases yet.")).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/knowledge-bases/oferta`)).status()).toBe(404);
});

test("AC-knowledge-bases-3: deleting the base being edited closes its form, so a Save cannot bring it back", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Offer");
    await addBase(request, bot, "druga", "Second");
    await page.goto(`/bots/${bot}/knowledge-bases`);

    const row = page.getByRole("row").filter({ hasText: "oferta" });
    await row.getByRole("button", { name: "Edit" }).click();
    await expect(page.getByLabel("Title")).toHaveValue("Offer");
    await row.getByRole("button", { name: "Delete" }).click();

    await expect(row).toHaveCount(0);
    await expect(page.getByLabel("Title")).toHaveCount(0);
    expect((await request.get(`/api/bots/${bot}/knowledge-bases/oferta`)).status()).toBe(404);
});

test("AC-knowledge-bases-9: a base with articles is not deleted, and the screen lists the articles", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Offer");
    const article = await request.put(`/api/bots/${bot}/articles/priceList?ifRevision=0`, {
        data: { knowledgeBase: "oferta", title: "Price list", content: "c", llmGuide: "g", description: "d" },
    });
    expect(article.status()).toBe(200);
    await page.goto(`/bots/${bot}/knowledge-bases`);

    await page.getByRole("row").filter({ hasText: "oferta" }).getByRole("button", { name: "Delete" }).click();

    await expect(page.getByRole("alert")).toContainText("'priceList' is in the knowledge base 'oferta'");
    await expect(page.getByRole("row").filter({ hasText: "oferta" })).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/knowledge-bases/oferta`)).status()).toBe(200);
});

test("AC-knowledge-base-language-2: a new base starts on en, the language chosen is saved and shown in the list and the form", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await page.getByRole("button", { name: "Add a knowledge base" }).click();
    await expect(page.locator('[data-field="language"] select')).toHaveValue("en");
    await expect(page.locator('[data-field="language"] select').locator("option")).toHaveText(["pl", "en"]);
    await page.getByLabel("Id").fill("oferta");
    await page.getByLabel("Title").fill("Oferta");

    await page.locator('[data-field="language"] select').selectOption("pl");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    const row = page.getByRole("row").filter({ hasText: "oferta" });
    await expect(row.getByRole("cell", { name: "pl", exact: true })).toBeVisible();
    expect(await (await request.get(`/api/bots/${bot}/knowledge-bases/oferta`)).json()).toMatchObject({ language: "pl" });
    await row.getByRole("button", { name: "Edit" }).click();
    await expect(page.locator('[data-field="language"] select')).toHaveValue("pl");
});

test("AC-resource-rename-4: Change id on a base keeps its articles in it and leaves the author on the base", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "offer", "Offer");
    const article = await request.put(`/api/bots/${bot}/articles/cennik?ifRevision=0`, {
        data: { knowledgeBase: "offer", title: "Price list", content: "C", llmGuide: "G", description: "D" },
    });
    expect(article.status()).toBe(200);
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await page.getByRole("row").filter({ hasText: "offer" }).getByRole("button", { name: "Edit" }).click();

    await page.getByRole("button", { name: "Change id" }).click();
    const dialog = page.getByRole("dialog", { name: "Change id" });
    await dialog.getByLabel("New id").fill("pricing");
    await expect(dialog).toContainText("cennik knowledgeBase");
    await dialog.getByRole("button", { name: "Change id" }).click();

    await expect(dialog).toBeHidden();
    await expect(page.getByRole("row").filter({ hasText: "pricing" })).toBeVisible();
    const stored = (await (await request.get(`/api/bots/${bot}/articles/cennik`)).json()) as { knowledgeBase: string };
    expect(stored.knowledgeBase).toBe("pricing");
});

test("AC-knowledge-bases-10, AC-resource-rename-28: unsaved changes ask before they are lost, and Change id waits for them", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Offer");
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await page.getByRole("row").filter({ hasText: "oferta" }).getByRole("button", { name: "Edit" }).click();
    await expect(page.getByRole("button", { name: "Change id" })).toBeEnabled();
    await page.getByLabel("Title").fill("Unsaved");
    const questions: string[] = [];
    let accept = false;
    page.on("dialog", (dialog) => {
        questions.push(dialog.message());
        if (accept) void dialog.accept();
        else void dialog.dismiss();
    });

    await expect(page.getByRole("button", { name: "Change id" })).toBeDisabled();
    await page.getByRole("button", { name: "Cancel" }).click();
    await page.getByRole("button", { name: "Add a knowledge base" }).click();
    await page.getByRole("link", { name: "Bots" }).click();
    await expect(page).toHaveURL(`/bots/${bot}/knowledge-bases`);
    await expect(page.getByLabel("Title")).toHaveValue("Unsaved");
    accept = true;
    await page.getByRole("button", { name: "Cancel" }).click();

    await expect(page.getByLabel("Title")).toHaveCount(0);
    expect(questions).toEqual(Array(4).fill("Discard the changes you have not saved?"));
});

test("AC-knowledge-bases-11: the address --open prints opens the form of that base", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Offer");

    await page.goto(`/bots/${bot}/knowledge-bases?open=oferta`);

    await expect(page.getByLabel("Title")).toHaveValue("Offer");
    await expect(page).toHaveURL(`/bots/${bot}/knowledge-bases`);
});
