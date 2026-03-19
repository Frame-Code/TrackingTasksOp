# Descripción de cada respuesta Json mapeadas a las entidades

Para la consulta:
```http request
GET {{baseUrl}}/api/v3/work_packages?pageSize=50&offset=1
Authorization: {{auth}}
Accept: application/json
```

La respuesta json que se mapean a entidades es la siguiente:
```json
{
  "_type": "WorkPackageCollection",
  "total": 32,
  "count": 32,
  "pageSize": 50,
  "offset": 1,
  "_embedded": {
    "elements": [
      {
        "id": 2,
        "subject": "Mi tarea 1",
        "description": {
          "format": "markdown",
          "raw": "",
          "html": ""
        },
        "startDate": "2026-03-16",
        "dueDate": "2026-03-30",
        "percentageDone": null,
        "createdAt": "2026-03-18T22:38:32.104Z",
        "updatedAt": "2026-03-18T22:38:34.035Z",
        "_links": {
          "addComment": {},
          "type": {
            "href": "/api/v3/types/3",
            "title": "Phase"
          },
          "priority": {
            "href": "/api/v3/priorities/8",
            "title": "Normal"
          },
          "project": {
            "href": "/api/v3/projects/1",
            "title": "Demo project"
          },
          "status": {
            "href": "/api/v3/statuses/7",
            "title": "In progress"
          },
          "assignee": {
            "href": "/api/v3/users/4",
            "title": "OpenProject Admin"
          }
        }
      }
    ]
  }
}
```