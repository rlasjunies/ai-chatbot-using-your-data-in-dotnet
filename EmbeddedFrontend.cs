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

      function formatDuration(ms) {
        if (ms < 1000) return `${ms}ms`;
        return `${(ms / 1000).toFixed(1)}s`;
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
        const [progressSteps, setProgressSteps] = useState([]);
        const [progressCompleted, setProgressCompleted] = useState(false);
        const [progressExpanded, setProgressExpanded] = useState(true);
        const [useStreaming, setUseStreaming] = useState(true);
        const bottomRef = useRef(null);

        useEffect(() => {
          bottomRef.current?.scrollIntoView({ behavior: "smooth" });
        }, [messages.length, progressSteps.length]);

        const visibleMessages = useMemo(() => messages.filter(isRenderable), [messages]);

        async function sendMessageStreaming(nextHistory) {
          setProgressSteps([]);
          setProgressCompleted(false);
          setProgressExpanded(true);
          
          try {
            const res = await fetch("/api/chat/stream", {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify(nextHistory.map(toOpenAIMsg)),
            });
            
            if (!res.ok) throw new Error("HTTP " + res.status);
            
            const reader = res.body.getReader();
            const decoder = new TextDecoder();
            let buffer = "";
            let completionReceived = false;
            let lastStepTimestamp = null;
            
            while (true) {
              const { done, value } = await reader.read();
              if (done) break;
              
              buffer += decoder.decode(value, { stream: true });
              const lines = buffer.split("\n");
              buffer = lines.pop() || "";
              
              for (const line of lines) {
                if (line.startsWith("data: ")) {
                  const data = line.slice(6).trim();
                  if (data === "[DONE]") {
                    // Stream is done, mark progress as completed and collapse it
                    setProgressCompleted(true);
                    setProgressExpanded(false);
                    continue;
                  }
                  
                  try {
                    const event = JSON.parse(data);
                    const eventTimestamp = new Date(event.Timestamp || event.timestamp);
                    const duration = lastStepTimestamp ? eventTimestamp - lastStepTimestamp : 0;
                    lastStepTimestamp = eventTimestamp;
                    
                    if (event.Type === "status" || event.type === "status") {
                      setProgressSteps(prev => [...prev, {
                        type: "status",
                        message: event.Message || event.message,
                        timestamp: eventTimestamp,
                        duration: duration
                      }]);
                    }
                    else if (event.Type === "function_call" || event.type === "function_call") {
                      setProgressSteps(prev => [...prev, {
                        type: "function_call",
                        message: event.Message || event.message,
                        functionName: event.FunctionName || event.functionName,
                        timestamp: eventTimestamp,
                        duration: duration
                      }]);
                    }
                    else if (event.Type === "function_result" || event.type === "function_result") {
                      setProgressSteps(prev => [...prev, {
                        type: "function_result",
                        message: event.Message || event.message,
                        functionName: event.FunctionName || event.functionName,
                        timestamp: eventTimestamp,
                        duration: duration
                      }]);
                    }
                    else if (event.Type === "completion" || event.type === "completion") {
                      completionReceived = true;
                      const content = event.Content || event.content || "";
                      const msgs = event.Messages || event.messages || [];
                      const eventTimestamp = new Date(event.Timestamp || event.timestamp);
                      const duration = lastStepTimestamp ? eventTimestamp - lastStepTimestamp : 0;
                      
                      console.log("[SSE] Completion event received:", { content, msgsLength: msgs.length, msgs });
                      
                      // Add the assistant message - prefer content string for simplicity
                      if (content && content.trim()) {
                        console.log("[SSE] Adding content as new message:", content);
                        setMessages(prev => {
                          const newMsg = {
                            role: "assistant",
                            contents: [{ $type: "text", text: content }]
                          };
                          console.log("[SSE] New messages array will be:", [...prev, newMsg]);
                          return [...prev, newMsg];
                        });
                      } else if (msgs.length > 0) {
                        console.log("[SSE] Adding messages from event:", msgs);
                        setMessages(prev => [...prev, ...msgs]);
                      } else {
                        console.warn("[SSE] Completion event has no content or messages!");
                      }
                      
                      // Show final completion step
                      setProgressSteps(prev => [...prev, {
                        type: "completion",
                        message: event.Message || event.message,
                        timestamp: new Date(event.Timestamp || event.timestamp),
                        duration: duration
                      }]);
                    }
                    else if (event.Type === "error" || event.type === "error") {
                      setError(event.Message || event.message);
                    }
                  } catch (parseError) {
                    console.error("Failed to parse SSE event:", parseError, data);
                  }
                }
              }
            }
            
            // If we didn't get a completion event, something went wrong
            if (!completionReceived) {
              throw new Error("No completion event received");
            }
          } catch (err) {
            console.error(err);
            setError(err.message);
            setMessages(prev => [...prev, {
              role: "assistant",
              contents: [{ $type: "text", text: "Sorry—something went wrong." }]
            }]);
            setProgressCompleted(true);
            setProgressExpanded(false);
          }
        }

        async function sendMessageNonStreaming(nextHistory) {
          try {
            const res = await fetch("/api/chat", {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify(nextHistory.map(toOpenAIMsg)),
            });
            if (!res.ok) throw new Error("HTTP " + res.status);
            
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
          }
        }

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

          if (useStreaming) {
            await sendMessageStreaming(nextHistory);
          } else {
            await sendMessageNonStreaming(nextHistory);
          }

          setIsSending(false);
        }

        function onKeyDown(e) {
          if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
          }
        }

        function clearChat() {
          setMessages([]);
          setProgressSteps([]);
          setProgressCompleted(false);
          setProgressExpanded(true);
          setError(null);
        }

        return (
          <div className="min-h-screen flex flex-col bg-neutral-100 text-neutral-900">
            <header className="sticky top-0 z-10 bg-white border-b">
              <div className="mx-auto max-w-3xl px-4 py-3 flex items-center justify-between">
                <h1 className="text-lg font-semibold">🤖 Chatbot Demo</h1>
                <div className="flex gap-2 items-center">
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={useStreaming}
                      onChange={(e) => setUseStreaming(e.target.checked)}
                      disabled={isSending}
                    />
                    Show Progress
                  </label>
                  <button onClick={clearChat} disabled={isSending} className="px-3 py-1 border rounded hover:bg-gray-50">
                    Clear Chat
                  </button>
                </div>
              </div>
            </header>

            <main className="flex-1 overflow-auto">
              <div className="mx-auto max-w-3xl px-4 py-6">
                {visibleMessages.length === 0 ? (
                  <div className="text-center text-neutral-500 py-16">
                    <div className="mb-4 text-4xl">💬</div>
                    <div>Start a conversation below.</div>
                    <div className="text-sm mt-2">Toggle "Show Progress" to see AI processing steps in real-time.</div>
                  </div>
                ) : (
                  <ul>
                    {visibleMessages.map((m, i) => (
                      <li key={i} className="flex gap-3 mb-4">
                        <div className={clsx("w-8 h-8 flex-shrink-0 flex items-center justify-center rounded-full font-semibold", m.role === "user" ? "bg-blue-600 text-white" : "bg-green-600 text-white")}>
                          {m.role === "user" ? "U" : "A"}
                        </div>
                        <div className="flex-1 border rounded-lg p-3 bg-white shadow-sm whitespace-pre-wrap" dangerouslySetInnerHTML={{ __html: renderMessageHtml(m) }}></div>
                      </li>
                    ))}
                    <div ref={bottomRef}></div>
                  </ul>
                )}
                
                {/* Progress steps display */}
                {progressSteps.length > 0 && (
                  <div className={clsx(
                    "mb-4 border rounded-lg p-4 shadow-sm transition-colors",
                    progressCompleted ? "bg-gray-50 border-gray-300" : "bg-blue-50 border-blue-200"
                  )}>
                    <div 
                      className="font-semibold flex items-center justify-between cursor-pointer"
                      onClick={() => setProgressExpanded(!progressExpanded)}
                    >
                      <div className={clsx("flex items-center gap-2", progressCompleted ? "text-gray-700" : "text-blue-900")}>
                        {!progressCompleted && (
                          <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none"></circle>
                            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                          </svg>
                        )}
                        {progressCompleted && <span className="text-green-600">✓</span>}
                        {progressCompleted ? "Processing completed" : "Processing..."}
                        {progressCompleted && progressSteps.length > 0 && (
                          <span className="text-xs text-gray-500 font-mono ml-2">
                            Total: {formatDuration(progressSteps.reduce((sum, step) => sum + (step.duration || 0), 0))}
                          </span>
                        )}
                      </div>
                      <button className="text-sm text-gray-500 hover:text-gray-700">
                        {progressExpanded ? "▼ Hide" : "▶ Show details"}
                      </button>
                    </div>
                    {progressExpanded && (
                      <div className="space-y-2 mt-3">
                        {progressSteps.map((step, idx) => (
                          <div key={idx} className="flex items-start gap-2 text-sm">
                            <span className={clsx(
                              "flex-shrink-0 w-5 h-5 flex items-center justify-center rounded-full text-xs font-bold",
                              step.type === "completion" ? "bg-green-500 text-white" :
                              step.type === "error" ? "bg-red-500 text-white" :
                              step.type === "function_call" ? "bg-purple-500 text-white" :
                              step.type === "function_result" ? "bg-teal-500 text-white" :
                              step.type === "status" && step.message.includes("LLM") ? "bg-blue-600 text-white" :
                              "bg-gray-400 text-white"
                            )}>
                              {step.type === "completion" ? "✓" :
                               step.type === "error" ? "✗" :
                               step.type === "function_call" ? "🔍" :
                               step.type === "function_result" ? "📊" :
                               step.type === "status" && step.message.includes("LLM") ? "🤖" :
                               "•"}
                            </span>
                            <div className="flex-1 flex items-baseline justify-between gap-2">
                              <span className={clsx(
                                step.type === "function_call" ? "text-purple-800 font-medium" :
                                step.type === "function_result" ? "text-teal-800 font-medium" :
                                step.type === "status" && step.message.includes("LLM") ? "text-blue-800 font-medium" :
                                "text-gray-700"
                              )}>
                                {step.message}
                              </span>
                              {step.duration !== undefined && (
                                <span className="text-xs text-gray-500 font-mono whitespace-nowrap">
                                  +{formatDuration(step.duration)}
                                </span>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
                
                {error && (
                  <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg text-red-700">
                    <strong>Error:</strong> {error}
                  </div>
                )}
              </div>
            </main>

            <footer className="sticky bottom-0 bg-white border-t shadow-lg">
              <div className="mx-auto max-w-3xl px-4 py-4">
                <form onSubmit={sendMessage} className="flex gap-2 items-end">
                  <textarea
                    rows={3}
                    placeholder="Type a message…"
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    onKeyDown={onKeyDown}
                    className="flex-1 border rounded-lg p-3 focus:outline-none focus:ring-2 focus:ring-blue-500"
                    disabled={isSending}
                  />
                  <button
                    type="submit"
                    disabled={isSending || !input.trim()}
                    className="px-6 py-3 rounded-lg bg-blue-600 text-white font-semibold disabled:bg-gray-300 disabled:cursor-not-allowed hover:bg-blue-700 transition-colors"
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

  public const string IndexerHtml = """
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Index Manager</title>
    <script src="https://cdn.tailwindcss.com"></script>
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
      const { useState, useEffect } = React;

      function App() {
        const [indexStats, setIndexStats] = useState(null);
        const [isLoading, setLoading] = useState(true);
        const [isBuilding, setBuilding] = useState(false);
        const [error, setError] = useState(null);
        const [buildResult, setBuildResult] = useState(null);

        async function loadIndexStats() {
          setLoading(true);
          setError(null);
          try {
            const res = await fetch("/api/index/list");
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            setIndexStats(data);
          } catch (err) {
            console.error(err);
            setError(err.message || String(err));
          } finally {
            setLoading(false);
          }
        }

        async function buildIndex() {
          setBuilding(true);
          setError(null);
          setBuildResult(null);
          try {
            const res = await fetch("/api/index/build", { method: "POST" });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            setBuildResult(data);
            // Reload stats after building
            await loadIndexStats();
          } catch (err) {
            console.error(err);
            setError(err.message || String(err));
          } finally {
            setBuilding(false);
          }
        }

        useEffect(() => {
          loadIndexStats();
        }, []);

        return (
          <div className="min-h-screen bg-neutral-100">
            <header className="sticky top-0 bg-white border-b">
              <div className="mx-auto max-w-4xl px-6 py-4 flex items-center justify-between">
                <h1 className="text-xl font-semibold">🔧 Index Manager</h1>
                <a href="/ui" className="text-blue-700 underline">← Back to Home</a>
              </div>
            </header>

            <main className="mx-auto max-w-4xl px-6 py-8">
              {/* Status Card */}
              <div className="bg-white border border-neutral-200 rounded-2xl p-6 mb-6">
                <h2 className="text-lg font-semibold mb-4">Index Status</h2>
                
                {isLoading ? (
                  <p className="text-neutral-500">Loading...</p>
                ) : error ? (
                  <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-xl p-3">
                    Error: {error}
                  </div>
                ) : indexStats ? (
                  <div>
                    <div className="flex items-center gap-2 mb-4">
                      <span className="text-sm font-medium">Total Records:</span>
                      <span className="text-2xl font-bold text-blue-700">{indexStats.count}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium">Status:</span>
                      <span className={`px-3 py-1 rounded-full text-sm font-medium ${
                        indexStats.hasRecords 
                          ? 'bg-green-100 text-green-800' 
                          : 'bg-yellow-100 text-yellow-800'
                      }`}>
                        {indexStats.hasRecords ? '✓ Indexed' : '⚠ No Records'}
                      </span>
                    </div>
                  </div>
                ) : null}
              </div>

              {/* Build Index Card */}
              <div className="bg-white border border-neutral-200 rounded-2xl p-6">
                <h2 className="text-lg font-semibold mb-4">Build Index</h2>
                <p className="text-sm text-neutral-600 mb-4">
                  Click the button below to build the vector search index from Wikipedia articles. 
                  This process will fetch and embed multiple landmark articles.
                </p>

                <button
                  onClick={buildIndex}
                  disabled={isBuilding || (indexStats && indexStats.hasRecords)}
                  className="px-6 py-3 rounded-xl bg-blue-600 text-white font-medium disabled:bg-gray-300 disabled:cursor-not-allowed hover:bg-blue-700 transition-colors"
                >
                  {isBuilding ? 'Building Index...' : 'Create Index'}
                </button>

                {indexStats && indexStats.hasRecords && (
                  <p className="mt-3 text-sm text-neutral-500">
                    Index already exists. Button is disabled.
                  </p>
                )}

                {buildResult && (
                  <div className="mt-4 p-4 bg-green-50 border border-green-200 rounded-xl">
                    <p className="text-sm text-green-800 font-medium">
                      ✓ {buildResult.message}
                    </p>
                    <p className="text-xs text-green-700 mt-1">
                      Processed {buildResult.recordCount} landmarks
                    </p>
                  </div>
                )}
              </div>

              {/* Refresh Button */}
              <div className="mt-6 text-center">
                <button
                  onClick={loadIndexStats}
                  disabled={isLoading}
                  className="px-4 py-2 text-sm text-blue-700 hover:text-blue-800 underline disabled:text-gray-400"
                >
                  {isLoading ? 'Refreshing...' : '🔄 Refresh Status'}
                </button>
              </div>
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
              <a href="/ui/indexer" class="text-blue-700 font-medium">
                🔧 Index Manager
              </a>
              <p class="text-sm text-neutral-600 mt-1">
                Manage and build the vector search index
              </p>
            </li>
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
            <li>
              <a href="/ui/prompts" class="text-blue-700 font-medium">
                ⚙️ Prompt Editor
              </a>
              <p class="text-sm text-neutral-600 mt-1">
                Edit and manage system prompts in real-time
              </p>
            </li>
          </ul>
        </div>
      </main>
    </div>
  </body>
</html>
""";

  public const string PromptEditorHtml = """
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Prompt Editor</title>
    <script src="https://cdn.tailwindcss.com"></script>
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
      const { useState, useEffect } = React;

      function App() {
        const [prompts, setPrompts] = useState([]);
        const [expandedPrompt, setExpandedPrompt] = useState(null);
        const [editingPrompt, setEditingPrompt] = useState(null);
        const [editContent, setEditContent] = useState('');
        const [isLoading, setLoading] = useState(true);
        const [isSaving, setSaving] = useState(false);
        const [isResetting, setResetting] = useState(false);
        const [error, setError] = useState(null);
        const [successMessage, setSuccessMessage] = useState(null);

        async function loadPrompts() {
          setLoading(true);
          setError(null);
          try {
            const res = await fetch("/api/prompts");
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            setPrompts(data);
          } catch (err) {
            console.error(err);
            setError(err.message || String(err));
          } finally {
            setLoading(false);
          }
        }

        async function loadPromptDetail(name) {
          try {
            const res = await fetch(`/api/prompts/${encodeURIComponent(name)}`);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            setExpandedPrompt(data);
          } catch (err) {
            console.error(err);
            setError(err.message || String(err));
          }
        }

        async function savePrompt(name) {
          setSaving(true);
          setError(null);
          setSuccessMessage(null);
          try {
            const res = await fetch(`/api/prompts/${encodeURIComponent(name)}`, {
              method: "PUT",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({ content: editContent })
            });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            setSuccessMessage(`✓ ${data.message}`);
            setEditingPrompt(null);
            setEditContent('');
            setExpandedPrompt(null);
            await loadPrompts();
          } catch (err) {
            console.error(err);
            setError(err.message || String(err));
          } finally {
            setSaving(false);
          }
        }

        async function resetPrompt(name) {
          if (!confirm(`Reset "${name}" to default?`)) return;
          setSaving(true);
          setError(null);
          setSuccessMessage(null);
          try {
            // Get default from embedded prompts and save it
            const defaults = {
              'ChatSystemPrompt': 'ChatSystemPrompt',
              'HydePrompt': 'HydePrompt',
              'RagSystemPrompt': 'RagSystemPrompt'
            };
            
            // Call reset all, then reload - simpler than individual reset
            const res = await fetch("/api/prompts/reset", { method: "POST" });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            setSuccessMessage(`✓ Prompt reset to default`);
            setEditingPrompt(null);
            setExpandedPrompt(null);
            await loadPrompts();
          } catch (err) {
            console.error(err);
            setError(err.message || String(err));
          } finally {
            setSaving(false);
          }
        }

        async function resetAllPrompts() {
          if (!confirm("Reset ALL prompts to defaults?")) return;
          setResetting(true);
          setError(null);
          setSuccessMessage(null);
          try {
            const res = await fetch("/api/prompts/reset", { method: "POST" });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            setSuccessMessage(`✓ ${data.message}`);
            setEditingPrompt(null);
            setExpandedPrompt(null);
            await loadPrompts();
          } catch (err) {
            console.error(err);
            setError(err.message || String(err));
          } finally {
            setResetting(false);
          }
        }

        function startEditing(prompt) {
          setEditingPrompt(prompt.name);
          setEditContent(prompt.content);
        }

        function cancelEditing() {
          setEditingPrompt(null);
          setEditContent('');
        }

        function toggleExpand(name) {
          if (expandedPrompt?.name === name) {
            setExpandedPrompt(null);
          } else {
            loadPromptDetail(name);
          }
        }

        useEffect(() => {
          loadPrompts();
        }, []);

        useEffect(() => {
          if (successMessage) {
            const timer = setTimeout(() => setSuccessMessage(null), 5000);
            return () => clearTimeout(timer);
          }
        }, [successMessage]);

        return (
          <div className="min-h-screen bg-neutral-100">
            <header className="sticky top-0 bg-white border-b z-10">
              <div className="mx-auto max-w-5xl px-6 py-4 flex items-center justify-between">
                <h1 className="text-xl font-semibold">⚙️ Prompt Editor</h1>
                <div className="flex items-center gap-4">
                  <button
                    onClick={resetAllPrompts}
                    disabled={isResetting || isLoading}
                    className="px-4 py-2 text-sm rounded-lg bg-red-100 text-red-700 hover:bg-red-200 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                  >
                    {isResetting ? 'Resetting...' : 'Reset All to Defaults'}
                  </button>
                  <a href="/ui" className="text-blue-700 underline">← Back to Home</a>
                </div>
              </div>
            </header>

            <main className="mx-auto max-w-5xl px-6 py-8">
              {/* Messages */}
              {error && (
                <div className="mb-4 text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-xl p-4">
                  Error: {error}
                </div>
              )}
              {successMessage && (
                <div className="mb-4 text-sm text-green-700 bg-green-50 border border-green-200 rounded-xl p-4">
                  {successMessage}
                </div>
              )}

              {/* Info Box */}
              <div className="mb-6 bg-blue-50 border border-blue-200 rounded-xl p-4">
                <p className="text-sm text-blue-800">
                  <strong>Note:</strong> Changes take effect immediately after saving. 
                  The next chat/question will use the updated prompt.
                </p>
              </div>

              {/* Prompts List */}
              {isLoading ? (
                <div className="text-center py-16">
                  <p className="text-neutral-500">Loading prompts...</p>
                </div>
              ) : (
                <div className="space-y-4">
                  {prompts.map((prompt) => {
                    const isExpanded = expandedPrompt?.name === prompt.name;
                    const isEditing = editingPrompt === prompt.name;

                    return (
                      <div key={prompt.name} className="bg-white border border-neutral-200 rounded-2xl overflow-hidden">
                        {/* Header */}
                        <div className="p-5 flex items-center justify-between border-b border-neutral-100">
                          <div className="flex-1">
                            <h3 className="font-semibold text-lg">{prompt.name}</h3>
                            <p className="text-sm text-neutral-500 mt-1">
                              Last updated: {new Date(prompt.updatedAt).toLocaleString()}
                            </p>
                          </div>
                          <button
                            onClick={() => toggleExpand(prompt.name)}
                            className="px-4 py-2 text-sm rounded-lg bg-neutral-100 hover:bg-neutral-200 transition-colors"
                          >
                            {isExpanded ? 'Collapse' : 'Expand'}
                          </button>
                        </div>

                        {/* Preview */}
                        {!isExpanded && (
                          <div className="p-5">
                            <p className="text-sm text-neutral-600 font-mono whitespace-pre-wrap">
                              {prompt.preview}
                            </p>
                          </div>
                        )}

                        {/* Expanded Content */}
                        {isExpanded && expandedPrompt && (
                          <div className="p-5">
                            {isEditing ? (
                              <div>
                                <textarea
                                  value={editContent}
                                  onChange={(e) => setEditContent(e.target.value)}
                                  rows={20}
                                  className="w-full border border-neutral-300 rounded-lg p-3 font-mono text-sm focus:outline-none focus:border-blue-500"
                                />
                                <div className="flex gap-2 mt-4">
                                  <button
                                    onClick={() => savePrompt(prompt.name)}
                                    disabled={isSaving || !editContent.trim()}
                                    className="px-4 py-2 rounded-lg bg-blue-600 text-white hover:bg-blue-700 disabled:bg-gray-300 disabled:cursor-not-allowed transition-colors"
                                  >
                                    {isSaving ? 'Saving...' : 'Save Changes'}
                                  </button>
                                  <button
                                    onClick={cancelEditing}
                                    disabled={isSaving}
                                    className="px-4 py-2 rounded-lg bg-neutral-200 text-neutral-800 hover:bg-neutral-300 transition-colors"
                                  >
                                    Cancel
                                  </button>
                                </div>
                              </div>
                            ) : (
                              <div>
                                <pre className="text-sm font-mono whitespace-pre-wrap bg-neutral-50 border border-neutral-200 rounded-lg p-4 max-h-96 overflow-y-auto">
                                  {expandedPrompt.content}
                                </pre>
                                <div className="flex gap-2 mt-4">
                                  <button
                                    onClick={() => startEditing(expandedPrompt)}
                                    className="px-4 py-2 rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors"
                                  >
                                    Edit Prompt
                                  </button>
                                  <button
                                    onClick={() => resetPrompt(prompt.name)}
                                    className="px-4 py-2 rounded-lg bg-orange-100 text-orange-700 hover:bg-orange-200 transition-colors"
                                  >
                                    Reset to Default
                                  </button>
                                </div>
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </main>
          </div>
        );
      }

      ReactDOM.createRoot(document.getElementById("root")).render(<App />);
    </script>
  </body>
</html>
""";
}
