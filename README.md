# bolagsdata-mcp

An [MCP](https://modelcontextprotocol.io) server that gives AI agents such as Claude Code, Claude Desktop, Cursor and GitHub Copilot access to Swedish company data. It is written in C# with the official MCP C# SDK.

It grew out of a real prospecting workflow. Every new lead needed the same manual checks: look the company up in the business register, check its size and marketing blocks, and find a phone number on its own website. With this server an agent does those checks itself, and the data rules below are enforced in code instead of in a prompt.

## Tools

| Tool | What it does | Source |
|---|---|---|
| `get_company` | Looks up a company by organisation number: name, address, municipality, industry (SNI), size and turnover class, ownership, registered phone and e-mail, and marketing blocks | Statistics Sweden (SCB), business register API |
| `find_phone_numbers` | Reads a company's start and contact pages and returns the phone numbers a visitor can see, with the pages they were found on | The company's own website |

All tools are marked read-only and idempotent, so clients can run them without asking for confirmation.

Example prompt in Claude Code:

> Look up 556012-5790 with get_company. If it has no telemarketing block, confirm a phone number on the company's own website.

## Design decisions

**Only legal entities.** A sole trader's organisation number is their personal identity number. `OrgNumber` rejects those (the third digit is a month, 0 or 1), so the server never looks up private individuals.

**Marketing blocks are enforced, not just passed on.** SCB records telemarketing blocks (telefonspärr) and advertising blocks (reklamspärr). The tool reports them as booleans and adds a plain-language note. A code only counts as "no block" when SCB's own code text says so. Unknown codes count as blocked.

**A phone number is confirmed only on the company's own site.** Register and directory numbers go stale. `find_phone_numbers` returns only numbers in visible text or `tel:` links. Numbers that only appear in scripts or structured metadata are ignored, because in practice one company's schema.org metadata listed another company's switchboard. The page title is returned so the agent can check that a guessed domain really belongs to the company.

**The agent chooses the URL, so the server limits where it can go.** `PublicNetwork` checks the resolved IP address when each connection is opened, including after redirects, and refuses loopback, private, link-local (cloud metadata) and other non-public ranges. Without this, a prompt could make the server read internal addresses (server-side request forgery).

**Secrets stay out of code and logs.** The SCB API key is read from the environment variable `SCB_AFR_API_KEY` and is never logged or returned. All logging goes to stderr, because stdout carries the MCP protocol.

**Respect the sources.** SCB allows five calls per second, so the client spaces its calls. SCB's code tables are loaded once and cached.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- For `get_company`: a free personal API key from SCB, available at <https://registreraafr.scb.se>. Store it as an environment variable:

  ```powershell
  [Environment]::SetEnvironmentVariable('SCB_AFR_API_KEY', '<your key>', 'User')
  ```

  `find_phone_numbers` works without a key.

## Build and test

```powershell
dotnet build -c Release
dotnet test -c Release --filter "Category!=Live"
```

`Category!=Live` skips the tests that call the real APIs. Run everything, including the live tests, with `dotnet test -c Release`.

## Use it with Claude Code

The repository includes a project-scoped `.mcp.json`. Build in Release, then start Claude Code in the repository folder and approve the `bolagsdata` server when asked.

To use the server from any folder, register it for your user instead. Use the absolute path to the DLL:

```powershell
claude mcp add bolagsdata --scope user -- dotnet "C:\path\to\bolagsdata-mcp\src\BolagsdataMcp\bin\Release\net10.0\BolagsdataMcp.dll"
```

Check that it is connected with `claude mcp list`, or with `/mcp` inside Claude Code.

## Project structure

```
src/BolagsdataMcp/
  Program.cs              Host, HTTP clients and MCP server setup
  OrgNumber.cs            Parsing and validation of organisation numbers
  Tools/                  The MCP tools and their output models
  Scb/                    SCB business register client, code tables
  Web/                    Website fetching, SSRF protection, phone number extraction
tests/BolagsdataMcp.Tests/
  Unit tests, a contract test that starts the server over stdio, and live API tests
```

## Data sources and terms

- SCB's business register (AFR) is free to use with a personal API key. See SCB's terms when you register.
- Website lookups fetch at most eight pages per site and only when the agent asks for one specific company. This is not a crawler.
- Follow Swedish marketing law and GDPR when you contact companies, including the blocks this server reports.
