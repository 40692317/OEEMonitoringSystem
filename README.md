OEE Monitoring System
A full-stack web platform for monitoring and analysing Overall Equipment Effectiveness (OEE), built as my final year project at Edinburgh Napier University (BSc Hons Computer Science).
What it does
The system tracks machine performance in real time and uses machine learning to predict OEE trends and flag equipment condition states (Normal / Warning / Critical) before issues escalate.
Tech stack
	•	Backend: C#, ASP.NET Core
	•	Database: PostgreSQL, Entity Framework Core
	•	Real-time updates: SignalR
	•	Machine Learning: ML.NET (regression and classification models)
	•	Frontend: Chart.js, jQuery, HTML/CSS
	•	Other: ClosedXML (Excel import/export), Serilog (logging)
Key features
	•	Real-time dashboards showing live equipment performance
	•	Automated OEE trend prediction using regression models
	•	Machine condition classification (Normal / Warning / Critical)
	•	Side-by-side comparison of multiple ML algorithms (FastTree, Random Forest, SDCA/Logistic Regression), with automatic selection of the best-performing model
	•	Excel import/export for production data and reporting
Why ML.NET
I evaluated three ML frameworks (ML.NET, scikit-learn, TensorFlow) against the project’s requirements before choosing ML.NET, since it integrates natively with the C#/.NET backend without needing a separate Python service.
<img width="1072" height="561" alt="post_1_trend" src="https://github.com/user-attachments/assets/a1fed1f9-c9cc-437b-acdc-0f0aba513588" />
<img width="1919" height="881" alt="post_2_dashboard" src="https://github.com/user-attachments/assets/26be4b14-60be-496a-9137-f863d8634fca" />
<img width="1919" height="881" alt="post_3_classification" src="https://github.com/user-attachments/assets/7ce5d7d6-9386-4e5a-8a1b-429645ee5b9b" />
<img width="1919" height="881" alt="post_4_regression" src="https://github.com/user-attachments/assets/edff70f3-91e9-4d01-abdd-094e83957890" />



