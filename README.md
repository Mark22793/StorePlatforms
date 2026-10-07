StorePlatforms - Microservices E-Commerce System

`StorePlatforms` is a distributed, event-driven e-commerce platform built using .NET 10 microservices architecture. It includes catalog management, inventory tracking, order processing, payment integration, and a consumer-facing web application.

Architecture Diagram

docx: https://docs.google.com/document/d/1T7wQ0pmFvYakp2fCYS8KfWdZo96kzVguLshTqXpwhfA/edit?usp=sharing

Prerequisites

Before running the solution, ensure you have the following installed on your machine:

.NET 10 SDK (or .NET 8/9 depending on your local SDK configuration)
SQL Server / LocalDB or MS SQL Server Express
Git
Service Port Mapping & Database Overview
Service / App	HTTP Port	HTTPS Port	Database / Persistence
CatalogService.Api	5124	7124	StorePlatforms_Catalog
InventoryService.Api	5002	7002	StorePlatforms_Inventory
OrderService.Api	5127	7001 / 7054	StorePlatforms_Order
PaymentService.Api	5003	7003	StorePlatforms_Payment
Storefront.Web	5000	5001	Front-End UI Client

Step-by-Step Setup & Execution (Without Docker)
1. Clone the Repository

git clone https://github.com/Mark22793/StorePlatforms.git

cd StorePlatforms

Team Contribution Matrix
Student Name	Role	Responsibilities & Deliverables
Mark Dave Cardenas	Backend Developer	Built and implemented backend services: CatalogService.Api, InventoryService.Api, OrderService.Api, and PaymentService.Api. Handled backend application logic, API endpoints, and service integrations.

Jomarie Callueng	Frontend & Integration Lead	Developed the Storefront.Web frontend application. Connected and integrated the frontend application with all backend API services. Handled frontend-to-backend HTTP communications and API requests.

Jherome Flores	Database & Documentation Lead	Configured and applied EF Core Database Migrations and Seeding for each service. Designed and created the System Architecture Diagram. Managed project documentation including README.md and CONTRIBUTIONS.md.


Validation screenshot:
https://docs.google.com/document/d/1s-kTA3V820AX4JELIpOTgRSwcToXOtaSen5tjxeiTZQ/edit?usp=sharing

