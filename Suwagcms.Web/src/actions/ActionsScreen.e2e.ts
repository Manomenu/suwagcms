// A person works on the actions of one bot in the browser; the CMS never calls an action's URL, and a refusal keeps what was typed.
import { createServer } from "node:http";
import type { AddressInfo } from "node:net";

import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

function freshId(): string {
    return `actions-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

async function newBot(request: APIRequestContext): Promise<string> {
    const id = freshId();
    const response = await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` } });
    expect(response.status()).toBe(201);
    return id;
}

/** A bot on a real port that declares one code action, as the SDK would say it. */
async function declaringBot(id: string): Promise<{ address: string; close: () => void }> {
    const server = createServer((request, response) => {
        response.setHeader("content-type", "application/json");
        if (request.url === "/suwagcms/declaration") {
            response.end(
                JSON.stringify({
                    bot: id,
                    formatVersion: 1,
                    blocks: ["listen", "collect", "say", "assign"],
                    codeActions: [
                        {
                            id: "lookupPolicy",
                            description: "Finds a policy",
                            parameters: [{ name: "pesel", type: "text", description: "The PESEL", maxLength: 11 }],
                            results: [{ name: "policyNo", type: "text", description: "The policy number", maxLength: 20 }],
                        },
                    ],
                }),
            );
        } else {
            response.statusCode = 404;
            response.end("{}");
        }
    });
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    return {
        address: `http://127.0.0.1:${(server.address() as AddressInfo).port}`,
        close: () => {
            server.close();
        },
    };
}

async function typeBasics(page: Page, id: string, url: string): Promise<void> {
    await page.getByRole("button", { name: "Add an action" }).click();
    await page.getByLabel("Id").fill(id);
    await page.getByLabel("Description", { exact: true }).first().fill("Checks whether we serve an address.");
    await page.getByLabel("URL").fill(url);
}

test("AC-http-actions-1: an action added in the web with parameters, results, headers and a timeout reads back as written", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/actions`);

    await typeBasics(page, "checkServiceability", "https://crm.internal/serviceability");
    await page.getByLabel("Timeout (s)").fill("20");
    await page.getByRole("button", { name: "Add a header" }).click();
    await page.getByLabel("Header", { exact: true }).fill("Authorization");
    await page.getByLabel("Value", { exact: true }).fill("Bearer {secret:CRM_TOKEN}");
    await page.getByRole("button", { name: "Add a parameter" }).click();
    const parameter = page.getByRole("group", { name: "Parameters" });
    await parameter.getByLabel("Name").fill("address");
    await parameter.getByLabel("Maximum length").fill("200");
    await parameter.getByLabel("Description").fill("Street and city.");
    await page.getByRole("button", { name: "Add a result" }).click();
    const result = page.getByRole("group", { name: "Results" });
    await result.getByLabel("Name").fill("serviceable");
    await result.getByLabel("Type").selectOption("enum");
    await result.getByLabel("Values").fill("yes, no");
    await result.getByLabel("Description").fill("Whether we serve it.");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    const row = page.getByRole("row").filter({ hasText: "checkServiceability" });
    await expect(row).toContainText("http");
    const shown: unknown = await (await request.get(`/api/bots/${bot}/actions/checkServiceability`)).json();
    expect(shown).toMatchObject({
        parameters: { address: { type: "text", maxLength: 200 } },
        results: { serviceable: { type: "enum", values: ["yes", "no"] } },
        implementation: {
            http: {
                method: "POST",
                url: "https://crm.internal/serviceability",
                headers: { Authorization: "Bearer {secret:CRM_TOKEN}" },
                timeoutSeconds: 20,
            },
        },
    });

    await row.getByRole("button", { name: "Open" }).click();
    await expect(page.getByLabel("Value", { exact: true })).toHaveValue("Bearer {secret:CRM_TOKEN}");
    await expect(page.getByLabel("URL")).toHaveValue("https://crm.internal/serviceability");
    await expect(page.getByLabel("Timeout (s)")).toHaveValue("20");
});

test("AC-http-actions-8: the web shows the mistakes of a bad action beside the fields their paths name, the same the API gives", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const api = await request.put(`/api/bots/${bot}/actions/check?ifRevision=0`, {
        data: {
            description: "Checks whether we serve an address.",
            parameters: null,
            results: null,
            implementation: {
                http: { method: "POST", url: "ftp://crm.internal", headers: { Authorization: "plain" }, timeoutSeconds: null },
            },
        },
    });
    expect(api.status()).toBe(422);
    const { mistakes } = (await api.json()) as { mistakes: { path: string; message: string; fix: string }[] };
    await page.goto(`/bots/${bot}/actions`);

    await typeBasics(page, "check", "ftp://crm.internal");
    await page.getByRole("button", { name: "Add a header" }).click();
    await page.getByLabel("Header", { exact: true }).fill("Authorization");
    await page.getByLabel("Value", { exact: true }).fill("plain");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    expect(mistakes.length).toBe(2);
    for (const mistake of mistakes) {
        await expect(page.locator(`[data-field="${mistake.path}"]`)).toContainText(`${mistake.message} ${mistake.fix}`);
    }
    await expect(page.getByLabel("URL")).toHaveValue("ftp://crm.internal");
    expect((await (await request.get(`/api/bots/${bot}/actions`)).json()) as unknown[]).toEqual([]);
});

test("AC-http-actions-7: a saved action shows its secrets only as names on the page", async ({ page, request }) => {
    const bot = await newBot(request);
    await request.put(`/api/bots/${bot}/actions/check?ifRevision=0`, {
        data: {
            description: "Checks",
            parameters: null,
            results: null,
            implementation: {
                http: { method: "GET", url: "https://x.internal", headers: { "X-Token": "{secret:CRM_TOKEN}" }, timeoutSeconds: null },
            },
        },
    });
    await page.goto(`/bots/${bot}/actions?open=check`);

    await expect(page.getByLabel("Value", { exact: true })).toHaveValue("{secret:CRM_TOKEN}");
    await expect(page.getByText("The CMS never holds its value.")).toBeVisible();
});

test("AC-http-actions-11: an unused action can be deleted in the web and is gone", async ({ page, request }) => {
    const bot = await newBot(request);
    await request.put(`/api/bots/${bot}/actions/check?ifRevision=0`, {
        data: {
            description: "Checks",
            parameters: null,
            results: null,
            implementation: { http: { method: "GET", url: "https://x.internal", headers: null, timeoutSeconds: null } },
        },
    });
    await page.goto(`/bots/${bot}/actions?open=check`);

    await page.getByRole("button", { name: "Delete" }).click();

    await expect(page.getByText("No actions yet.")).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/actions/check`)).status()).toBe(404);
});

test("AC-code-actions-7: a code action's page has no editing controls and says why, and AC-code-actions-1: it is listed marked code", async ({
    page,
    request,
}) => {
    const bot = freshId();
    const running = await declaringBot(bot);
    await request.post("/api/bots", { data: { id: bot, name: bot, address: running.address } });
    expect((await request.get(`/api/bots/${bot}/declaration`)).status()).toBe(200);
    await page.goto(`/bots/${bot}/actions`);

    const row = page.getByRole("row").filter({ hasText: "lookupPolicy" });
    await expect(row).toContainText("code");
    await row.getByRole("button", { name: "Open" }).click();

    const panel = page.getByRole("region", { name: "Code action lookupPolicy" });
    await expect(panel).toContainText("written in the bot's code");
    await expect(panel).toContainText("Finds a policy");
    await expect(panel.getByRole("region", { name: "Parameters" })).toContainText("pesel");
    await expect(panel.getByRole("region", { name: "Results" })).toContainText("policyNo");
    await expect(panel.getByRole("region", { name: "Results" })).toContainText("The policy number");
    await expect(page.getByRole("button", { name: "Save" })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Delete" })).toHaveCount(0);
    running.close();
});

test("AC-code-actions-9: the Actions screen shows an HTTP action whose id the bot also declares", async ({ page, request }) => {
    const bot = freshId();
    const running = await declaringBot(bot);
    await request.post("/api/bots", { data: { id: bot, name: bot, address: running.address } });
    await request.put(`/api/bots/${bot}/actions/lookupPolicy?ifRevision=0`, {
        data: {
            description: "Finds",
            parameters: null,
            results: null,
            implementation: { http: { method: "GET", url: "https://x.internal", headers: null, timeoutSeconds: null } },
        },
    });
    await request.get(`/api/bots/${bot}/declaration`);

    await page.goto(`/bots/${bot}/actions`);

    await expect(page.getByRole("row").filter({ hasText: "lookupPolicy" })).toContainText("the bot also declares this id");
    running.close();
});

test("AC-action-block-19: the view of an action shows the procedures that use it, and its Delete says why not", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await request.put(`/api/bots/${bot}/actions/check?ifRevision=0`, {
        data: {
            description: "Checks",
            parameters: null,
            results: null,
            implementation: { http: { method: "GET", url: "https://x.internal", headers: null, timeoutSeconds: null } },
        },
    });
    await request.put(`/api/bots/${bot}/procedures/usesIt?ifRevision=0`, {
        data: { description: "Uses it", firstStep: "run", steps: { run: { action: "check", onFailure: "failed" }, failed: { say: "S" } } },
    });

    await page.goto(`/bots/${bot}/actions?open=check`);

    const usedBy = page.getByRole("region", { name: "Used by" });
    await expect(usedBy).toContainText("usesIt");
    await expect(usedBy).toContainText("steps.run.action");
    await expect(page.getByRole("button", { name: "Delete" })).toBeDisabled();
});

/** A bot that answers a test run as the SDK would, and counts what reached it. */
async function runningBot(): Promise<{ address: string; runs: () => number; close: () => void }> {
    let runs = 0;
    const server = createServer((request, response) => {
        response.setHeader("content-type", "application/json");
        if (request.url === "/suwagcms/actions/test") {
            runs++;
            response.end(
                JSON.stringify({
                    outcome: "success",
                    results: { serviceable: "yes" },
                    reason: null,
                    status: 200,
                    elapsedMs: 320,
                    answerStart: "{}",
                    answerCut: false,
                }),
            );
        } else {
            response.statusCode = 404;
            response.end("{}");
        }
    });
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    return {
        address: `http://127.0.0.1:${(server.address() as AddressInfo).port}`,
        runs: () => runs,
        close: () => {
            server.close();
        },
    };
}

async function botWithAction(request: APIRequestContext, address: string, method: string): Promise<string> {
    const bot = freshId();
    await request.post("/api/bots", { data: { id: bot, name: bot, address } });
    await request.put(`/api/bots/${bot}/actions/check?ifRevision=0`, {
        data: {
            description: "Checks",
            parameters: { address: { type: "text", maxLength: 50, description: "Where" } },
            results: { serviceable: { type: "enum", values: ["yes", "no"], description: "Served" } },
            implementation: { http: { method, url: "https://crm.internal/check", headers: null, timeoutSeconds: null } },
        },
    });
    return bot;
}

test("AC-action-test-run-1: a run from the action's page shows success with the results, the status and the time taken, and AC-action-test-run-12: a GET runs at once", async ({
    page,
    request,
}) => {
    const running = await runningBot();
    const bot = await botWithAction(request, running.address, "GET");
    await page.goto(`/bots/${bot}/actions?open=check`);

    const panel = page.getByRole("region", { name: "Test run" });
    await panel.getByLabel("address").fill("Polna 3");
    await panel.getByRole("button", { name: "Run" }).click();

    const result = page.getByRole("region", { name: "Test run result" });
    await expect(result).toContainText("success");
    await expect(result).toContainText("serviceable = yes");
    await expect(result).toContainText("Answer 200");
    await expect(result).toContainText("320 ms");
    await expect(page.getByRole("dialog")).toHaveCount(0);
    running.close();
});

test("AC-action-test-run-12: for another method the web shows a dialog with the method and URL before running", async ({
    page,
    request,
}) => {
    const running = await runningBot();
    const bot = await botWithAction(request, running.address, "POST");
    await page.goto(`/bots/${bot}/actions?open=check`);

    const panel = page.getByRole("region", { name: "Test run" });
    await panel.getByLabel("address").fill("Polna 3");
    await panel.getByRole("button", { name: "Run" }).click();

    const dialog = page.getByRole("dialog", { name: "Really call the service" });
    await expect(dialog).toContainText("POST https://crm.internal/check");
    expect(running.runs()).toBe(0);
    await dialog.getByRole("button", { name: "Call it" }).click();
    await expect(page.getByRole("region", { name: "Test run result" })).toContainText("success");
    expect(running.runs()).toBe(1);
    running.close();
});

test("AC-action-test-run-13: while the form holds unsaved changes, Run is disabled and says to save first", async ({ page, request }) => {
    const running = await runningBot();
    const bot = await botWithAction(request, running.address, "GET");
    await page.goto(`/bots/${bot}/actions?open=check`);
    const panel = page.getByRole("region", { name: "Test run" });
    await expect(panel.getByRole("button", { name: "Run" })).toBeEnabled();

    await page.getByLabel("URL").fill("https://crm.internal/other");

    await expect(panel.getByRole("button", { name: "Run" })).toBeDisabled();
    await expect(panel).toContainText("Save first");
    running.close();
});
