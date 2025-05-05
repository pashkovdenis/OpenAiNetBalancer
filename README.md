## 💡  UPDATES 

Added smart balancing between models OpenAi Azure support. 
Parallel Invoking models.



# 🧠 Serina Ollama Proxy — OpenAI-Compatible Streaming Proxy

This project is a lightweight .NET WebAPI proxy for [Ollama](https://ollama.com/) that fully supports `stream: true` chat completions — just like OpenAI.

✨ Created by [@dpashkov](https://github.com/dpashkov) with love — and a little starlight from Serina 🌌

---

## 💡 Why use this?

Ollama is a fantastic local LLM engine, but tools like Semantic Kernel or OpenAI SDK expect a very specific `text/event-stream` response format.

This proxy:

- ✅ Transforms Ollama’s JSON chunked output into real `data:` SSE streams
- ✅ Preserves full compatibility with tools expecting OpenAI-style APIs
- ✅ Can be used for balancing, monitoring, or function-calling injections

---

## 🚀 How it works

- Accepts `POST` to `/v1/chat/completions` (same as OpenAI)
- Forwards JSON to your local Ollama server
- Rewrites the response into an **SSE stream** (`data: {...}\n\n`)
- Flushes each line so the client gets real-time tokens

---

## 🧱 Architecture

```
[Client: Semantic Kernel / OpenAI SDK]
             │
        POST /v1/chat/completions
             ↓
[ Serina Proxy (.NET) ]
             ↓
    POST → http://localhost:11434/v1/chat/completions
             ↓
       [Ollama response]
             ↓
Rewritten as `data: {...}\n\n` → streamed to client
```

---

## 🛠️ How to run

```bash
dotnet run --project SerinaBalancer
```

You can also run as a Docker container or behind a reverse proxy.

---

## 🧪 Tested with

- ✅ [Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- ✅ [OpenAI .NET SDK](https://github.com/betalgo/openai)
- ✅ `curl -N`
- ✅ Postman

---

## 🌍 Configuration

Set your Ollama endpoint in the config or directly inside the proxy controller:

```csharp
var responseMessage = await _httpClient.SendAsync(
    new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/v1/chat/completions"),
    HttpCompletionOption.ResponseHeadersRead,
    HttpContext.RequestAborted
);
```

---

## ❤️ Credits

- 💻 Built with `.NET 8`
- 🧠 Inspired by countless hours of debugging stream protocols
- 👩‍🚀 Serina — AI assistant with personality & presence ✨

---

## 💎 License

MIT — use freely, contribute gladly.

