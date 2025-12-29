namespace ChatBot;

public static class EmbeddedFrontend
{
    public const string ChatHtml = """
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Chatbot Demo</title>
    <!-- Tailwind (Play CDN) for utility classes used below -->
    <script src="https://cdn.tailwindcss.com"></script>
    <!-- React and ReactDOM from CDN -->
    <script src="https://unpkg.com/react@18/umd/react.development.js" crossorigin></script>
    <script src="https://unpkg.com/react-dom@18/umd/react-dom.development.js" crossorigin></script>
    <script src="https://unpkg.com/@babel/standalone/babel.min.js"></script>
    <style>
      body {
        margin: 0;
        font-family: system-ui, sans-serif;
      }
    </style>
  </head>
  <body>
    <div id="root"></div>

    <script type="text/babel">
      const { useState, useEffect, useRef, useMemo } = React;

      function clsx(...parts) {
        return parts.filter(Boolean).join(" ");
      }

      function escapeHtml(unsafe) {
        return String(unsafe)
          .replaceAll(/&/g, "&amp;")
          .replaceAll(/</g, "&lt;")
          .replaceAll(/>/g, "&gt;")
          .replaceAll(/\"/g, "&quot;")
          .replaceAll(/'/g, "&#039;");
      }

      // Very simple markdown link support: [text](url) or [text] (url)
      function renderMarkdownLinks(escapedText) {
        const mdLink = /\[([^\]]+)\]\s*\((https?:\/\/[^\s)]+)\)/g;
        return escapedText.replace(mdLink, (m, text, href) => {
          const safeHref = href.replaceAll('"', '%22');
          return `<a href="${safeHref}" target="_blank" rel="noopener noreferrer" class="text-blue-700 underline">${text}</a>`;
        });
      }

      function renderMessageHtml(msg) {
        const raw = getTextFromMessage(msg);
        const escaped = escapeHtml(raw);
        return renderMarkdownLinks(escaped);
      }

      function getTextFromMessage(msg) {
        if (!msg) return "";
        const blocks = Array.isArray(msg.contents)
          ? msg.contents
          : msg.content
          ? [{ $type: "text", text: String(msg.content) }]
          : [];
        return (
          blocks
            .filter(
              (b) => b && (b.$type === "text" || b.type === "text") && typeof b.text === "string"
            )
            .map((b) => b.text)
            .join("\n")
            .trim()
        );
      }

      function isRenderable(msg) {
        if (msg.role !== "user" && msg.role !== "assistant") return false;
        return getTextFromMessage(msg).length > 0;
      }

      function toOpenAIMsg(msg) {
        if (!msg) return msg;
        if (Array.isArray(msg.contents)) return msg;
        if (typeof msg.content === "string" && msg.content.trim().length > 0) {
          return {
            role: msg.role,
            contents: [{ $type: "text", text: msg.content }],
          };
        }
        // default empty
        return { role: msg.role, contents: [] };
      }

      function App() {
        const [messages, setMessages] = useState([]);
        const [input, setInput] = useState("");
        const [isSending, setIsSending] = useState(false);
        const [error, setError] = useState(null);
        const bottomRef = useRef(null);

        useEffect(() => {
          bottomRef.current?.scrollIntoView({ behavior: "smooth" });
        }, [messages.length]);

        const visibleMessages = useMemo(() => messages.filter(isRenderable), [messages]);

        async function sendMessage(e) {
          e?.preventDefault();
          if (!input.trim() || isSending) return;
          setError(null);

          const userMsg = {
            role: "user",
            contents: [
              {
                $type: "text",
                text: input.trim(),
              },
            ],
          };
          const nextHistory = [...messages, userMsg];
          setMessages(nextHistory);
          setInput("");
          setIsSending(true);

          try {
            const res = await fetch("/api/chat", {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify(nextHistory.map(toOpenAIMsg)),
            });
            if (!res.ok) throw new Error("HTTP " + res.status);
            // Be tolerant: backend may return
            // - an array of messages
            // - an object with { messages: [...] }
            // - a single message object
            // - plain text
            const contentType = res.headers.get("content-type") || "";
            if (contentType.includes("application/json")) {
              const data = await res.json();
              const msgs = Array.isArray(data)
                ? data
                : Array.isArray(data?.messages)
                ? data.messages
                : data && data.role
                ? [data]
                : [];
              if (msgs.length === 0) throw new Error("Bad response");
              setMessages((prev) => [...prev, ...msgs]);
            } else {
              const text = await res.text();
              if (text && text.trim().length) {
                setMessages((prev) => [
                  ...prev,
                  { role: "assistant", contents: [{ $type: "text", text }] },
                ]);
              } else {
                throw new Error("Empty response");
              }
            }
          } catch (err) {
            console.error(err);
            setError(err.message);
            setMessages((prev) => [
              ...prev,
              {
                role: "assistant",
                contents: [{ $type: "text", text: "Sorry—something went wrong." }],
              },
            ]);
          } finally {
            setIsSending(false);
          }
        }

        function onKeyDown(e) {
          if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
          }
        }

        function clearChat() {
          setMessages([]);
          setError(null);
        }

        return (
          <div className="min-h-screen flex flex-col bg-neutral-100 text-neutral-900">
            <header className="sticky top-0 z-10 bg-white border-b">
              <div className="mx-auto max-w-3xl px-4 py-3 flex items-center justify-between">
                <h1 className="text-lg font-semibold">🤖 Chatbot Demo</h1>
                <button onClick={clearChat} disabled={isSending}>Clear Chat</button>
              </div>
            </header>

            <main className="flex-1">
              <div className="mx-auto max-w-3xl px-4 py-6">
                {visibleMessages.length === 0 ? (
                  <div className="text-center text-neutral-500 py-16">Start a conversation below.</div>
                ) : (
                  <ul>
                    {visibleMessages.map((m, i) => (
                      <li key={i} className="flex gap-3 mb-2">
                        <div className={clsx("w-8 h-8 flex items-center justify-center", m.role === "user" ? "bg-blue-600 text-white" : "bg-green-600 text-white")}>
                          {m.role === "user" ? "U" : "A"}
                        </div>
                        <div className="flex-1 border rounded p-2 whitespace-pre-wrap" dangerouslySetInnerHTML={{ __html: renderMessageHtml(m) }}></div>
                      </li>
                    ))}
                    <div ref={bottomRef}></div>
                  </ul>
                )}
                {error && <div style={{ color: "red" }}>Error: {error}</div>}
              </div>
            </main>

            <footer className="sticky bottom-0 bg-white border-t">
              <div className="mx-auto max-w-3xl px-4 py-4">
                <form onSubmit={sendMessage} className="flex gap-2 items-end">
                  <textarea
                    rows={3}
                    placeholder="Type a message…"
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    onKeyDown={onKeyDown}
                    className="flex-1 border rounded p-2"
                  />
                  <button
                    type="submit"
                    disabled={isSending || !input.trim()}
                    className="px-4 py-2 rounded bg-blue-600 text-white disabled:bg-gray-300"
                  >
                    {isSending ? "Sending…" : "Send"}
                  </button>
                </form>
              </div>
            </footer>
          </div>
        );
      }

      ReactDOM.createRoot(document.getElementById("root")).render(<App />);
    </script>
  </body>
</html>
""";

    public const string QuestionHtml = """
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Ask a Question</title>
    <!-- Tailwind (Play CDN) -->
    <script src="https://cdn.tailwindcss.com"></script>
    <!-- React and ReactDOM from CDN -->
    <script src="https://unpkg.com/react@18/umd/react.development.js" crossorigin></script>
    <script src="https://unpkg.com/react-dom@18/umd/react-dom.development.js" crossorigin></script>
    <script src="https://unpkg.com/@babel/standalone/babel.min.js"></script>
    <style>
      body { margin: 0; font-family: system-ui, sans-serif; }
    </style>
  </head>
  <body>
    <div id="root"></div>

    <script type="text/babel">
      const { useState } = React;

      // Escape HTML to prevent injection
      function escapeHtml(unsafe) {
        return unsafe
          .replaceAll(/&/g, "&amp;")
          .replaceAll(/</g, "&lt;")
          .replaceAll(/>/g, "&gt;")
          .replaceAll(/\"/g, "&quot;")
          .replaceAll(/'/g, "&#039;");
      }

      // Convert URLs in the text to clickable links. Assumes input is already HTML-escaped.
      function linkify(escaped) {
        const urlRegex = /(https?:\/\/[^\s)]+|www\.[^\s)]+)/g;
        return escaped.replace(urlRegex, (m) => {
          const href = m.startsWith("http") ? m : `http://${m}`;
          return `<a href="${href}" target="_blank" rel="noopener noreferrer" class="text-blue-700 underline">${m}</a>`;
        });
      }

      // Normalize answer: trim wrapping quotes, unescape \n to real newlines, escape HTML, then linkify
      function formatAnswer(raw) {
        if (raw == null) return "";
        let s = String(raw).trim();
        // Trim matching surrounding quotes if present
        if ((s.startsWith('"') && s.endsWith('"')) || (s.startsWith("'") && s.endsWith("'"))) {
          s = s.slice(1, -1);
        }
        // Replace literal \n with actual newline characters
        s = s.replaceAll(/\\n/g, "\n");
        // Escape HTML before linkifying
        const escaped = escapeHtml(s);
        // Linkify URLs
        return linkify(escaped);
      }

      function App() {
        const [question, setQuestion] = useState("");
        const [isLoading, setLoading] = useState(false);
        const [error, setError] = useState(null);
        const [answer, setAnswer] = useState("");

        async function onAsk(e) {
          e?.preventDefault();
          const q = question.trim();
          if (!q) return;
          setLoading(true);
          setError(null);
          setAnswer("");
          try {
            const url = `/api/ask?question=${encodeURIComponent(q)}`;
            const res = await fetch(url, { method: "GET" });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);

            // Try to handle both JSON and plain text responses gracefully
            const contentType = res.headers.get("content-type") || "";
            if (contentType.includes("application/json")) {
              const data = await res.json();
              // Try common fields; fallback to stringifying
              const maybeAnswer =
                data?.answer ?? data?.result ?? data?.text ?? data?.message ?? "";
              setAnswer(formatAnswer(maybeAnswer || JSON.stringify(data)));
            } else {
              const text = await res.text();
              setAnswer(formatAnswer(text));
            }
          } catch (err) {
            console.error(err);
            setError(err.message || String(err));
          } finally {
            setLoading(false);
          }
        }

        return (
          <div className="min-h-screen bg-neutral-100">
            <header className="sticky top-0 bg-white border-b">
              <div className="mx-auto max-w-4xl px-6 py-4 flex items-center justify-between">
                <h1 className="text-xl font-semibold">❓ Ask a Question</h1>
              </div>
            </header>

            <main className="mx-auto max-w-4xl px-6 py-8">
              {/* Question form */}
              <form onSubmit={onAsk} className="flex items-stretch gap-2">
                <input
                  type="text"
                  placeholder="Type your question…"
                  className="flex-1 rounded-xl border border-neutral-300 px-4 py-3 text-[16px] focus:outline-none focus:border-neutral-400 bg-white"
                  value={question}
                  onChange={(e) => setQuestion(e.target.value)}
                />
                <button
                  type="submit"
                  disabled={isLoading || !question.trim()}
                  className="px-5 py-3 rounded-xl bg-blue-600 text-white disabled:bg-gray-300"
                >
                  {isLoading ? "Asking…" : "Ask"}
                </button>
              </form>

              {/* Status */}
              {error && (
                <div className="mt-4 text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-xl p-3">
                  Error: {error}
                </div>
              )}

              {/* Answer */}
              <section className="mt-8">
                {answer ? (
                  <div
                    className="bg-white border border-neutral-200 rounded-2xl p-5 whitespace-pre-wrap"
                    dangerouslySetInnerHTML={{ __html: answer }}
                  />
                ) : (
                  !isLoading && (
                    <p className="text-neutral-500">No answer yet. Ask something above.</p>
                  )
                )}
              </section>
            </main>
          </div>
        );
      }

      ReactDOM.createRoot(document.getElementById("root")).render(<App />);
    </script>
  </body>
</html>
""";

    public const string SearchChunksHtml = """
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Search Demo</title>
    <!-- Tailwind (Play CDN) -->
    <script src="https://cdn.tailwindcss.com"></script>
    <!-- React and ReactDOM from CDN -->
    <script src="https://unpkg.com/react@18/umd/react.development.js" crossorigin></script>
    <script src="https://unpkg.com/react-dom@18/umd/react-dom.development.js" crossorigin></script>
    <script src="https://unpkg.com/@babel/standalone/babel.min.js"></script>
    <style>
      body { margin: 0; font-family: system-ui, sans-serif; }
      a { text-decoration: underline; }
    </style>
  </head>
  <body>
    <div id="root"></div>

    <script type="text/babel">
      const { useState } = React;

      function App() {
        const [q, setQ] = useState("");
        const [isLoading, setLoading] = useState(false);
        const [error, setError] = useState(null);
        const [results, setResults] = useState([]);

        async function onSearch(e) {
          e?.preventDefault();
          if (!q.trim()) return;
          setLoading(true);
          setError(null);
          setResults([]);
          try {
            const res = await fetch(`/api/search?query=${encodeURIComponent(q.trim())}`, {
              method: "GET",
              headers: { "Content-Type": "application/json" }
            });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            if (!Array.isArray(data)) throw new Error("Expected an array of results");
            setResults(data);
          } catch (err) {
            console.error(err);
            setError(err.message || String(err));
          } finally {
            setLoading(false);
          }
        }

        return (
          <div className="min-h-screen bg-neutral-100">
            <header className="sticky top-0 bg-white border-b">
              <div className="mx-auto max-w-4xl px-6 py-4 flex items-center justify-between">
                <h1 className="text-xl font-semibold">🔎 Search Demo</h1>
              </div>
            </header>

            <main className="mx-auto max-w-4xl px-6 py-8">
              {/* Search bar */}
              <form onSubmit={onSearch} className="flex items-stretch gap-2">
                <input
                  type="text"
                  placeholder="Search the web…"
                  className="flex-1 rounded-xl border border-neutral-300 px-4 py-3 text-[16px] focus:outline-none focus:border-neutral-400 bg-white"
                  value={q}
                  onChange={(e) => setQ(e.target.value)}
                />
                <button
                  type="submit"
                  disabled={isLoading || !q.trim()}
                  className="px-5 py-3 rounded-xl bg-blue-600 text-white disabled:bg-gray-300"
                >
                  {isLoading ? "Searching…" : "Search"}
                </button>
              </form>

              {/* Status */}
              {error && (
                <div className="mt-4 text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-xl p-3">
                  Error: {error}
                </div>
              )}

              {/* Results */}
              <section className="mt-8">
                {results.length === 0 && !isLoading ? (
                  <p className="text-neutral-500">No results yet. Try a search.</p>
                ) : (
                  <ul className="space-y-5">
                    {results.map((r, i) => (
                      <li key={i} className="bg-white border border-neutral-200 rounded-2xl p-4">
                        <a href={r.sourcePageUrl} target="_blank" rel="noreferrer" className="text-blue-700 font-semibold">
                          {r.title} ({r.section} part {r.chunkIndex})
                        </a>
                        <p className="mt-1 text-[15px] text-neutral-800">{r.content}</p>
                        <p className="mt-1 text-xs text-neutral-500 break-all">{r.sourcePageUrl}</p>
                      </li>)
                    )}
                  </ul>
                )}
              </section>
            </main>
          </div>
        );
      }

      ReactDOM.createRoot(document.getElementById("root")).render(<App />);
    </script>
  </body>
</html>
""";

    public const string SearchLandmarksHtml = """
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Search Demo</title>
    <!-- Tailwind (Play CDN) -->
    <script src="https://cdn.tailwindcss.com"></script>
    <!-- React and ReactDOM from CDN -->
    <script src="https://unpkg.com/react@18/umd/react.development.js" crossorigin></script>
    <script src="https://unpkg.com/react-dom@18/umd/react-dom.development.js" crossorigin></script>
    <script src="https://unpkg.com/@babel/standalone/babel.min.js"></script>
    <style>
      body { margin: 0; font-family: system-ui, sans-serif; }
      a { text-decoration: underline; }
    </style>
  </head>
  <body>
    <div id="root"></div>

    <script type="text/babel">
      const { useState } = React;

      function truncate200(s) {
        if (!s) return "";
        return s.length > 200 ? s.slice(0, 200) + "…" : s;
      }

      function App() {
        const [q, setQ] = useState("");
        const [isLoading, setLoading] = useState(false);
        const [error, setError] = useState(null);
        const [results, setResults] = useState([]);

        async function onSearch(e) {
          e?.preventDefault();
          if (!q.trim()) return;
          setLoading(true);
          setError(null);
          setResults([]);
          try {
            const res = await fetch(`/api/search?query=${encodeURIComponent(q.trim())}`, {
              method: "GET",
              headers: { "Content-Type": "application/json" }
            });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            if (!Array.isArray(data)) throw new Error("Expected an array of results");
            setResults(data);
          } catch (err) {
            console.error(err);
            setError(err.message || String(err));
          } finally {
            setLoading(false);
          }
        }

        return (
          <div className="min-h-screen bg-neutral-100">
            <header className="sticky top-0 bg-white border-b">
              <div className="mx-auto max-w-4xl px-6 py-4 flex items-center justify-between">
                <h1 className="text-xl font-semibold">🔎 Search Demo</h1>
              </div>
            </header>

            <main className="mx-auto max-w-4xl px-6 py-8">
              {/* Search bar */}
              <form onSubmit={onSearch} className="flex items-stretch gap-2">
                <input
                  type="text"
                  placeholder="Search the web…"
                  className="flex-1 rounded-xl border border-neutral-300 px-4 py-3 text-[16px] focus:outline-none focus:border-neutral-400 bg-white"
                  value={q}
                  onChange={(e) => setQ(e.target.value)}
                />
                <button
                  type="submit"
                  disabled={isLoading || !q.trim()}
                  className="px-5 py-3 rounded-xl bg-blue-600 text-white disabled:bg-gray-300"
                >
                  {isLoading ? "Searching…" : "Search"}
                </button>
              </form>

              {/* Status */}
              {error && (
                <div className="mt-4 text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-xl p-3">
                  Error: {error}
                </div>
              )}

              {/* Results */}
              <section className="mt-8">
                {results.length === 0 && !isLoading ? (
                  <p className="text-neutral-500">No results yet. Try a search.</p>
                ) : (
                  <ul className="space-y-5">
                    {results.map((r, i) => (
                      <li key={i} className="bg-white border border-neutral-200 rounded-2xl p-4">
                        <a href={r.pageUrl} target="_blank" rel="noreferrer" className="text-blue-700 font-semibold">
                          {r.title || r.pageUrl}
                        </a>
                        <p className="mt-1 text-[15px] text-neutral-800">{truncate200(r.content)}</p>
                        <p className="mt-1 text-xs text-neutral-500 break-all">{r.pageUrl}</p>
                      </li>)
                    )}
                  </ul>
                )}
              </section>
            </main>
          </div>
        );
      }

      ReactDOM.createRoot(document.getElementById("root")).render(<App />);
    </script>
  </body>
</html>
""";

    public const string IndexHtml = """
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Chatbot Frontend</title>
    <script src="https://cdn.tailwindcss.com"></script>
    <style>
      body { margin: 0; font-family: system-ui, sans-serif; }
      a { text-decoration: underline; }
    </style>
  </head>
  <body class="bg-neutral-100">
    <div class="min-h-screen">
      <header class="bg-white border-b">
        <div class="mx-auto max-w-4xl px-6 py-4">
          <h1 class="text-xl font-semibold">🤖 AI Chatbot Demo - Frontend Pages</h1>
        </div>
      </header>

      <main class="mx-auto max-w-4xl px-6 py-8">
        <div class="bg-white border border-neutral-200 rounded-2xl p-6">
          <h2 class="text-lg font-semibold mb-4">Available Pages:</h2>
          <ul class="space-y-3">
            <li>
              <a href="/ui/searchlandmarks" class="text-blue-700 font-medium">
                1. Search Landmarks
              </a>
              <p class="text-sm text-neutral-600 mt-1">
                Search service that indexed only the introduction of each Wikipedia article
              </p>
            </li>
            <li>
              <a href="/ui/searchchunks" class="text-blue-700 font-medium">
                2. Search Chunks
              </a>
              <p class="text-sm text-neutral-600 mt-1">
                Search service that indexed the entire article and split it into sections and chunks
              </p>
            </li>
            <li>
              <a href="/ui/question" class="text-blue-700 font-medium">
                3. Question Answering (RAG)
              </a>
              <p class="text-sm text-neutral-600 mt-1">
                Question answering service using Retrieval-Augmented Generation
              </p>
            </li>
            <li>
              <a href="/ui/chat" class="text-blue-700 font-medium">
                4. Chatbot
              </a>
              <p class="text-sm text-neutral-600 mt-1">
                The final chatbot interface with full conversation support
              </p>
            </li>
          </ul>
        </div>
      </main>
    </div>
  </body>
</html>
""";
}
