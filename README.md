🤖 AI ChatBot Application

Built with .NET Core, ML.NET & SignalR

📌 Overview

This project is an AI-powered chatbot application developed using ASP.NET Core, ML.NET, and SignalR.
It supports real-time chat, sentiment analysis, FAQ handling, and recommendation suggestions, making it suitable for HR portals, internal support systems, or enterprise assistants.

The chatbot can:

Answer user questions (e.g., leave application process)

Analyze user sentiment (Positive / Negative / Neutral)

Provide contextual recommendations

Communicate in real time using SignalR
<img width="1905" height="1102" alt="image" src="https://github.com/user-attachments/assets/b3000b70-5c40-46e5-9f36-fb53c598a6c9" />

🛠️ Technology Stack
Technology	Description
.NET Core	Backend framework
ML.NET	Machine learning (sentiment analysis)
SignalR	Real-time messaging
ASP.NET Core MVC / Web API	Application architecture
HTML / CSS / JavaScript	Frontend UI
Bootstrap	UI styling
✨ Features

🔄 Real-Time Chat using SignalR

🧠 Sentiment Analysis using ML.NET

❓ Question & Answer Support

📊 Recommendation Engine (Leave policy, benefits, holidays, etc.)

🧑‍💼 HR Assistant Use Case

🖥️ Clean & Responsive UI

📷 Application Screenshot

(Replace path if needed or remove if not uploading image)

📂 Project Structure
├── Controllers
│   └── ChatController.cs
├── Hubs
│   └── ChatHub.cs
├── MLModels
│   └── SentimentModel.zip
├── Services
│   └── SentimentAnalysisService.cs
├── Views
│   └── Chat
│       └── Index.cshtml
├── wwwroot
│   ├── css
│   └── js
├── Program.cs
├── Startup.cs
└── README.md

⚙️ Installation & Setup
1️⃣ Prerequisites

.NET SDK 6.0 or later

Visual Studio 2022 / VS Code

Git

2️⃣ Clone the Repository
git clone https://github.com/your-username/ai-chatbot-dotnet.git
cd ai-chatbot-dotnet

3️⃣ Restore Dependencies
dotnet restore

4️⃣ Run the Application
dotnet run


Application will be available at:

http://localhost:7201

🧠 Machine Learning (ML.NET)

Uses ML.NET sentiment analysis model

Detects user mood (Positive / Negative)

Example:

Input: "I feel frustrated with payroll delays"
Output: Sentiment detected: Negative

🔄 SignalR Real-Time Communication

Enables instant message delivery

Supports multi-user chat scenarios

No page refresh required

🧪 Sample Inputs
Mode	Example
Question	How do I apply for leave?
Sentiment	I feel frustrated with payroll delays
Recommendation	Recommend a plan
Forecast	Predict headcount
🚀 Future Enhancements

✅ Authentication & Role-based Access

🌐 NLP integration with Azure Cognitive Services

📈 Advanced analytics dashboard

🗣️ Voice-based chatbot

☁️ Cloud deployment (Azure)

🤝 Contributing

Contributions are welcome!
Please fork the repository and submit a pull request.

📄 License

This project is licensed under the MIT License.
