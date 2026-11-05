// A person works on the articles of one bot in the browser; the bot is in the address, and a refusal or a stale edit keeps what was typed.
import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

function freshId(): string {
    return `art-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

async function newBot(request: APIRequestContext): Promise<string> {
    const id = freshId();
    const response = await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` } });
    expect(response.status()).toBe(201);
    for (const [base, title] of [
        ["offer", "Offer"],
        ["forValuers", "For valuers"],
    ]) {
        const created = await request.put(`/api/bots/${id}/knowledge-bases/${base}?ifRevision=0`, { data: { title } });
        expect(created.status()).toBe(200);
    }
    return id;
}

async function addArticle(
    request: APIRequestContext,
    bot: string,
    id: string,
    extra: { knowledgeBase?: string; language?: string; title?: string; description?: string; content?: string } = {},
): Promise<void> {
    const response = await request.put(`/api/bots/${bot}/articles/${id}?ifRevision=0`, {
        data: {
            knowledgeBase: extra.knowledgeBase ?? "offer",
            language: extra.language ?? null,
            title: extra.title ?? "Price list",
            content: extra.content ?? "What a valuation costs.",
            llmGuide: "Ask which kind of property first.",
            description: extra.description ?? "What a valuation costs",
        },
    });
    expect(response.status()).toBe(200);
}

async function fill(page: Page, id: string, title: string): Promise<void> {
    await page.getByLabel("Id", { exact: true }).fill(id);
    await page.getByLabel("Title", { exact: true }).fill(title);
    await page.getByLabel(/^content/).fill("What a valuation costs.");
    await page.getByLabel(/^llmGuide/).fill("Ask which kind of property first.");
    await page.getByLabel(/^description/).fill("What a valuation costs and what affects the price");
}

test("AC-articles-1: an article created in the web is listed in its base and is in the API, and AC-articles-10: the editor shows the three texts at once", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "New article" }).click();
    for (const label of [/^content/, /^llmGuide/, /^description/]) {
        await expect(page.getByLabel(label)).toBeVisible();
    }

    await fill(page, "priceList", "Price list");
    await page.locator('[data-field="knowledgeBase"] select').selectOption("forValuers");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    const row = page.getByRole("listitem").filter({ hasText: "priceList" });
    await expect(row).toContainText("Price list");
    await expect(row).toContainText("priceList · forValuers");
    const listed = await request.get(`/api/bots/${bot}/articles?knowledgeBase=forValuers`);
    expect(await listed.json()).toMatchObject([{ id: "priceList", content: "What a valuation costs." }]);
});

test("AC-articles-9: the label of content, llmGuide and description carries a tooltip saying who reads it", async ({ page, request }) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "New article" }).click();

    await expect(page.locator('[data-field="content"] label')).toHaveAttribute("title", /customer reads it, through the model/);
    await expect(page.locator('[data-field="llmGuide"] label')).toHaveAttribute("title", /Only the model reads it/);
    await expect(page.locator('[data-field="description"] label')).toHaveAttribute("title", /search reads this.*customer never sees it/);
});

test("AC-articles-2: a bad article shows every mistake beside its field and keeps what was typed", async ({ page, request }) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "New article" }).click();
    await page.getByLabel("Id", { exact: true }).fill("cennik");
    await page.getByLabel("Title", { exact: true }).fill("Price list");

    await page.getByRole("button", { name: "Add", exact: true }).click();

    for (const field of ["content", "llmGuide", "description"]) {
        await expect(page.locator(`[data-field="${field}"]`)).toContainText(`'cennik' has no ${field}.`);
    }
    await expect(page.getByLabel("Title", { exact: true })).toHaveValue("Price list");
    expect((await (await request.get(`/api/bots/${bot}/articles`)).json()) as unknown[]).toEqual([]);
});

test("AC-articles-3: an id another article has, even in another base, is refused beside the id", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "priceList", { knowledgeBase: "forValuers" });
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "New article" }).click();

    await fill(page, "priceList", "Another");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    await expect(page.locator('[data-field="id"]')).toContainText("already exists");
    await expect(page.getByLabel("Title", { exact: true })).toHaveValue("Another");
});

test("AC-articles-6: an edit in the web changes the article and a new base moves it", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "priceList");
    await page.goto(`/bots/${bot}/articles`);

    await page.getByRole("listitem").filter({ hasText: "priceList" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel("Title", { exact: true }).fill("New title");
    await page.locator('[data-field="knowledgeBase"] select').selectOption("forValuers");
    await page.getByRole("button", { name: "Save" }).click();

    const row = page.getByRole("listitem").filter({ hasText: "priceList" });
    await expect(row).toContainText("New title");
    await expect(row).toContainText("priceList · forValuers");
});

test("AC-articles-7: saving an edit of an article that changed meanwhile is refused with the current revision and keeps what was typed", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "priceList", { description: "First" });
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("listitem").filter({ hasText: "priceList" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel(/^description/).fill("Marta's text");
    const other = await request.put(`/api/bots/${bot}/articles/priceList?ifRevision=1`, {
        data: {
            knowledgeBase: "offer",
            title: "Price list",
            content: "c",
            llmGuide: "g",
            description: "The agent's text",
        },
    });
    expect(other.status()).toBe(200);

    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("alert")).toContainText("it is now revision 2");
    await expect(page.getByLabel(/^description/)).toHaveValue("Marta's text");
    await page.getByRole("button", { name: "Reload the article" }).click();
    await expect(page.getByRole("listitem").filter({ hasText: "priceList" })).toContainText("The agent's text");
    await expect(page.getByLabel(/^description/)).toHaveValue("Marta's text");
    await page.getByRole("button", { name: "Save" }).click();
    await expect(page.getByRole("listitem").filter({ hasText: "priceList" })).toContainText("Marta's text");
});

test("AC-articles-8: an article deleted in the web is gone from the list and from the API", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "priceList");
    await page.goto(`/bots/${bot}/articles`);

    await page.getByRole("listitem").filter({ hasText: "priceList" }).getByRole("button", { name: "Edit" }).click();
    await page.getByRole("button", { name: "Delete" }).click();

    await expect(page.getByText("No articles yet.")).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/articles/priceList`)).status()).toBe(404);
});

test("AC-articles-11: the list filters by base and by text, the same as the API does", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "discounts", { title: "Prices", description: "Is there a RABAT" });
    await addArticle(request, bot, "other", { title: "Other", description: "Unrelated", content: "A rabat is a discount." });
    await addArticle(request, bot, "elsewhere", { knowledgeBase: "forValuers", title: "Rabat", description: "Rabat" });
    await page.goto(`/bots/${bot}/articles`);
    await expect(page.getByRole("listitem")).toHaveCount(3);

    await page.getByLabel("Search").fill("rabat");
    await expect(page.getByRole("listitem")).toHaveCount(2);
    await page.getByLabel("Knowledge base", { exact: true }).selectOption("offer");

    await expect(page.getByRole("listitem")).toHaveCount(1);
    await expect(page.getByRole("listitem")).toContainText("discounts");
    const api = await request.get(`/api/bots/${bot}/articles?knowledgeBase=offer&text=rabat`);
    expect(((await api.json()) as { id: string }[]).map((article) => article.id)).toEqual(["discounts"]);
});

test("AC-articles-13: leaving the editor with unsaved changes asks before discarding them", async ({ page, request }) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "New article" }).click();
    await page.getByLabel("Title", { exact: true }).fill("Unsaved");
    const questions: string[] = [];
    let accept = false;
    page.on("dialog", (dialog) => {
        questions.push(dialog.message());
        if (accept) void dialog.accept();
        else void dialog.dismiss();
    });

    await page.getByRole("button", { name: "Cancel" }).click();
    await expect(page.getByLabel("Title", { exact: true })).toHaveValue("Unsaved");
    await page.getByRole("link", { name: "Bots" }).click();
    await expect(page).toHaveURL(`/bots/${bot}/articles`);
    accept = true;
    await page.getByRole("button", { name: "Cancel" }).click();

    await expect(page.getByLabel("Title", { exact: true })).toHaveCount(0);
    expect(questions).toEqual(Array(3).fill("Discard the changes you have not saved?"));
});

test("AC-articles-13: closing an untouched editor asks nothing", async ({ page, request }) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "New article" }).click();
    page.on("dialog", () => {
        throw new Error("no question was expected");
    });

    await page.getByRole("button", { name: "Cancel" }).click();

    await expect(page.getByLabel("Title", { exact: true })).toHaveCount(0);
});

test("AC-articles-15: the articles screen of one bot shows nothing of another bot's articles", async ({ page, request }) => {
    const first = await newBot(request);
    const second = await newBot(request);
    await addArticle(request, first, "priceList", { title: "Of the first" });

    await page.goto(`/bots/${second}/articles`);

    await expect(page.getByText("No articles yet.")).toBeVisible();
    await expect(page.getByText("Of the first")).toHaveCount(0);
});

test("AC-article-language-2: a new article starts on en, the language chosen is saved and shown in the list and the editor", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "New article" }).click();
    await expect(page.locator('[data-field="language"] select')).toHaveValue("en");
    await expect(page.locator('[data-field="language"] select').locator("option")).toHaveText(["pl", "en"]);
    await fill(page, "cennik", "Cennik");

    await page.locator('[data-field="language"] select').selectOption("pl");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    const row = page.getByRole("listitem").filter({ hasText: "cennik" });
    await expect(row).toContainText("cennik · forValuers · pl");
    expect(await (await request.get(`/api/bots/${bot}/articles/cennik`)).json()).toMatchObject({ language: "pl" });
    await row.getByRole("button", { name: "Edit" }).click();
    await expect(page.locator('[data-field="language"] select')).toHaveValue("pl");
});

test("AC-article-language-2: an article added without choosing keeps en", async ({ page, request }) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "New article" }).click();
    await fill(page, "pricing", "Pricing");

    await page.getByRole("button", { name: "Add", exact: true }).click();

    await expect(page.getByRole("listitem").filter({ hasText: "pricing" })).toContainText("pricing · forValuers · en");
});

test("AC-article-language-8: the label of the language select carries a tooltip saying what the field is for", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "New article" }).click();

    await expect(page.locator('[data-field="language"] label')).toHaveAttribute("title", /language the article is written in/);
});

test("AC-article-language-6: the list filters by language together with the other filters, the same as the API does", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { language: "pl", title: "Cennik", description: "Ile kosztuje wycena" });
    await addArticle(request, bot, "pricing", { title: "Pricing", description: "What a valuation costs" });
    await addArticle(request, bot, "cennikBiegly", {
        knowledgeBase: "forValuers",
        language: "pl",
        title: "Cennik",
        description: "Ile kosztuje",
    });
    await page.goto(`/bots/${bot}/articles`);
    await expect(page.getByRole("listitem")).toHaveCount(3);

    await page.getByLabel("Language", { exact: true }).selectOption("pl");
    await expect(page.getByRole("listitem")).toHaveCount(2);
    await page.getByLabel("Knowledge base", { exact: true }).selectOption("offer");

    await expect(page.getByRole("listitem")).toHaveCount(1);
    await expect(page.getByRole("listitem")).toContainText("cennik · offer · pl");
    const api = await request.get(`/api/bots/${bot}/articles?language=pl&knowledgeBase=offer`);
    expect(((await api.json()) as { id: string }[]).map((article) => article.id)).toEqual(["cennik"]);
});

test("AC-article-language-7: two articles in different languages with one title are two ordinary articles", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { language: "pl", title: "Cennik" });
    await addArticle(request, bot, "pricing", { language: "en", title: "Cennik" });
    await page.goto(`/bots/${bot}/articles`);

    await page.getByRole("listitem").filter({ hasText: "pricing" }).getByRole("button", { name: "Edit" }).click();
    await page.getByRole("button", { name: "Delete" }).click();

    await expect(page.getByRole("listitem")).toHaveCount(1);
    await expect(page.getByRole("listitem")).toContainText("cennik · offer · pl");
});

test("AC-articles-4: editing another field of an article whose text has CRLF line breaks leaves the stored text byte for byte", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const content = "# Cennik\r\n\r\n- zażółć gęślą jaźń\r\n\n\n";
    await addArticle(request, bot, "priceList", { content });
    await page.goto(`/bots/${bot}/articles`);

    await page.getByRole("listitem").filter({ hasText: "priceList" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel("Title", { exact: true }).fill("Edited title");
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("listitem").filter({ hasText: "priceList" })).toContainText("Edited title");
    expect(await (await request.get(`/api/bots/${bot}/articles/priceList`)).json()).toMatchObject({ title: "Edited title", content });
});

async function openEditor(page: Page, bot: string, id: string): Promise<void> {
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("listitem").filter({ hasText: id }).getByRole("button", { name: "Edit" }).click();
    await expect(page.getByRole("textbox", { name: "content" })).toBeVisible();
}

async function contentOf(request: APIRequestContext, bot: string, id: string): Promise<string> {
    return ((await (await request.get(`/api/bots/${bot}/articles/${id}`)).json()) as { content: string }).content;
}

test("AC-article-links-1: choosing an article in the picker puts a chip with its title in the text and stores the Markdown link there", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { title: "Price list" });
    await addArticle(request, bot, "wycena", { title: "Valuation", content: "See the " });
    await openEditor(page, bot, "wycena");

    await page.getByRole("textbox", { name: "content" }).click();
    await page.keyboard.press("Control+End");
    await page.locator('[data-markdown-editor="content"]').getByRole("button", { name: "Insert link" }).click();
    await page.keyboard.type("cen");
    await page
        .getByRole("listbox", { name: "Articles to link" })
        .getByRole("option", { name: /Price list/ })
        .click();
    await page.keyboard.type("now.");

    await expect(page.locator('[data-markdown-editor="content"] [data-article-link="cennik"]')).toHaveText("Price list");
    await page.getByRole("button", { name: "Save" }).click();
    await expect(page.getByRole("textbox", { name: "content" })).toHaveCount(0);
    expect(await contentOf(request, bot, "wycena")).toBe("See the [Price list](cms://article/cennik) now.");
});

test("AC-article-links-2: the picker lists the articles of every base of this bot, narrows by title or id, leaves out the article being edited and shows none of another bot", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const other = await newBot(request);
    await addArticle(request, bot, "cennik", { title: "Price list", knowledgeBase: "offer" });
    await addArticle(request, bot, "rules", { title: "Rules of valuers", knowledgeBase: "forValuers" });
    await addArticle(request, bot, "wycena", { title: "Valuation" });
    await addArticle(request, other, "secret", { title: "Secret price" });
    await openEditor(page, bot, "wycena");
    const options = page.getByRole("listbox", { name: "Articles to link" }).getByRole("option");

    await page.getByRole("textbox", { name: "content" }).click();
    await page.locator('[data-markdown-editor="content"]').getByRole("button", { name: "Insert link" }).click();
    await expect(options).toHaveText([/Price list/, /Rules of valuers/]);
    await page.keyboard.type("rul");
    await expect(options).toHaveText([/Rules of valuers/]);
    await page.keyboard.press("Backspace");
    await page.keyboard.press("Backspace");
    await page.keyboard.press("Backspace");
    await page.keyboard.type("cenn");
    await expect(options).toHaveText([/Price list/]);
    await page.keyboard.press("Backspace");
    await page.keyboard.press("Backspace");
    await page.keyboard.press("Backspace");
    await page.keyboard.press("Backspace");
    await page.keyboard.type("secret");
    await expect(page.getByText("No article matches")).toBeVisible();
});

test("AC-article-links-3: opening an article with links and saving another field leaves its text as stored", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik");
    const content =
        "# Heading\n\n* star list\n* with [Prices](cms://article/cennik)\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\nSome _emphasis_.\r\n";
    await addArticle(request, bot, "wycena", { content });
    await openEditor(page, bot, "wycena");

    await page.getByLabel("Title", { exact: true }).fill("Edited");
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("listitem").filter({ hasText: "wycena" })).toContainText("Edited");
    expect(await contentOf(request, bot, "wycena")).toBe(content);
});

test("AC-article-links-4: a chip shows the current title of its target, while the stored label stays", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { title: "Price list" });
    await addArticle(request, bot, "wycena", { content: "See [Price list](cms://article/cennik)." });
    await request.put(`/api/bots/${bot}/articles/cennik?ifRevision=1`, {
        data: { knowledgeBase: "offer", title: "Prices", content: "c", llmGuide: "g", description: "d" },
    });
    await openEditor(page, bot, "wycena");

    await expect(page.locator('[data-article-link="cennik"]')).toHaveText("Prices");
    await page.getByLabel("Title", { exact: true }).fill("Valuation");
    await page.getByRole("button", { name: "Save" }).click();
    await expect(page.getByRole("listitem").filter({ hasText: "wycena" })).toContainText("Valuation");
    expect(await contentOf(request, bot, "wycena")).toBe("See [Price list](cms://article/cennik).");
});

test("AC-article-links-5: clicking a chip opens its article, and deleting a chip removes the link from the saved text", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { title: "Price list" });
    await addArticle(request, bot, "wycena", { content: "See [Price list](cms://article/cennik)" });
    await openEditor(page, bot, "wycena");

    await page.getByRole("textbox", { name: "content" }).click();
    await page.keyboard.press("Control+End");
    await page.keyboard.press("Backspace");
    await expect(page.locator("[data-article-link]")).toHaveCount(0);
    await page.getByRole("button", { name: "Save" }).click();
    await expect(page.getByRole("textbox", { name: "content" })).toHaveCount(0);
    expect(await contentOf(request, bot, "wycena")).toBe("See ");

    await openEditor(page, bot, "cennik");
    await expect(page.getByLabel("Id", { exact: true })).toHaveValue("cennik");
    await request.put(`/api/bots/${bot}/articles/wycena?ifRevision=2`, {
        data: {
            knowledgeBase: "offer",
            title: "Valuation",
            content: "See [Price list](cms://article/cennik)",
            llmGuide: "g",
            description: "d",
        },
    });
    await openEditor(page, bot, "wycena");
    await page.locator('[data-article-link="cennik"]').click();
    await expect(page.getByLabel("Id", { exact: true })).toHaveValue("cennik");
    await expect(page.getByLabel("Title", { exact: true })).toHaveValue("Price list");
});

test("AC-article-links-6: a link saved as Markdown through the API, as the CLI does, is a chip in the web, in content and in llmGuide", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { title: "Price list" });
    const saved = await request.put(`/api/bots/${bot}/articles/wycena?ifRevision=0`, {
        data: {
            knowledgeBase: "offer",
            title: "Valuation",
            content: "See [Price list](cms://article/cennik).",
            llmGuide: "Mention [it](cms://article/cennik).",
            description: "d",
        },
    });
    expect(saved.status()).toBe(200);
    await openEditor(page, bot, "wycena");

    await expect(page.locator('[data-markdown-editor="content"] [data-article-link="cennik"]')).toHaveText("Price list");
    await expect(page.locator('[data-markdown-editor="llmGuide"] [data-article-link="cennik"]')).toHaveText("Price list");
});

test("AC-delete-guard-1: an article another article links to is refused for delete in the API and its Delete button is disabled in the web, AC-delete-guard-9", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik");
    await addArticle(request, bot, "wycena", { content: "See [it](cms://article/cennik)." });

    const refused = await request.delete(`/api/bots/${bot}/articles/cennik`);
    expect(refused.status()).toBe(422);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("listitem").filter({ hasText: "cennik" }).getByRole("button", { name: "Edit" }).click();

    await expect(page.getByRole("button", { name: "Delete" })).toBeDisabled();
    await expect(page.getByText("Used in 1 place. Remove that reference first.")).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/articles/cennik`)).status()).toBe(200);
});

test("AC-delete-guard-7: the panels list who uses the article and what it uses as working links, and say so when there are none", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "warunki");
    await addArticle(request, bot, "cennik", { content: "See [w](cms://article/warunki)." });
    await addArticle(request, bot, "wycena", { content: "See [c](cms://article/cennik)." });
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("listitem").filter({ hasText: "cennik" }).getByRole("button", { name: "Edit" }).click();

    await expect(page.getByRole("region", { name: "Used by" })).toContainText("wycena");
    await expect(page.getByRole("region", { name: "Used by" })).toContainText("content");
    await expect(page.getByRole("region", { name: "Uses" })).toContainText("warunki");
    await page.getByRole("region", { name: "Used by" }).getByRole("link", { name: "wycena" }).click();

    await expect(page.getByLabel("Id", { exact: true })).toHaveValue("wycena");
    await expect(page.getByRole("region", { name: "Used by" })).toContainText("Nothing uses this.");
});

test("AC-delete-guard-4: after the last link is removed in the web the article can be deleted", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik");
    await addArticle(request, bot, "wycena", { content: "See [it](cms://article/cennik)." });
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("listitem").filter({ hasText: "wycena" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel(/^content/).fill("No link now.");
    await page.getByRole("button", { name: "Save" }).click();
    await page.getByRole("listitem").filter({ hasText: "cennik" }).getByRole("button", { name: "Edit" }).click();

    await expect(page.getByRole("region", { name: "Used by" })).toContainText("Nothing uses this.");
    await page.getByRole("button", { name: "Delete" }).click();

    await expect(page.getByRole("listitem")).toHaveCount(1);
    expect((await request.get(`/api/bots/${bot}/articles/cennik`)).status()).toBe(404);
});
