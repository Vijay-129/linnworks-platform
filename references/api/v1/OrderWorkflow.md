<!-- REVERSE-DOCUMENTED by scripts/reverse_document_controller.py. No PublicApiSpecs file exists for this controller - this was derived from the old repo's working C# code, not from a Linnworks-published spec. Lower confidence than sync_api_spec.py output: no rate limits, no official descriptions. If Linnworks publishes a spec for this controller, delete this file and run sync_api_spec.py instead. -->

# OrderWorkflow (v1, reverse-documented)

Source: `LinnworksAPI/Controllers/OrderWorkflow.cs`  
_Last synced: 2026-08-13_

## Endpoints

| Method | Path | C# signature |
|---|---|---|
| POST | `/api/OrderWorkflow/CheckinUser` | `CheckinUserResponse CheckinUser(CheckinUserRequest request)` |
| POST | `/api/OrderWorkflow/DeallocateOrderFromJob` | `DeallocateOrderFromJobResponse DeallocateOrderFromJob(DeallocateOrderFromJobRequest request)` |
| POST | `/api/OrderWorkflow/GetGroup` | `GetGroupResponse GetGroup(GetGroupRequest request)` |
| POST | `/api/OrderWorkflow/GetGroupList` | `GetGroupListResponse GetGroupList(GetGroupListRequest request)` |
| POST | `/api/OrderWorkflow/GetJob` | `GetJobResponse GetJob(GetJobRequest request)` |
| POST | `/api/OrderWorkflow/GetJobAudit` | `GetJobAuditResponse GetJobAudit(GetJobAuditRequest request)` |
| POST | `/api/OrderWorkflow/GetJobByName` | `GetJobResponse GetJobByName(GetJobByNameRequest request)` |
| POST | `/api/OrderWorkflow/GetJobErrors` | `GetJobErrorsResponse GetJobErrors(GetJobErrorsRequest request)` |
| POST | `/api/OrderWorkflow/GetPrintAttachment` | `GetPrintAttachmentResponse GetPrintAttachment(GetPrintAttachmentRequest request)` |
| POST | `/api/OrderWorkflow/GetWorkflow` | `GetWorkflowResponse GetWorkflow(GetWorkflowRequest request)` |
| POST | `/api/OrderWorkflow/Run` | `RunResponse Run(RunJobsRequest request)` |
| POST | `/api/OrderWorkflow/UpdateGroup` | `UpdateGroupResponse UpdateGroup(UpdateGroupRequest request)` |

### POST `/api/OrderWorkflow/CheckinUser`

Checkin and start order allocation

- `request`: 

`CheckinUserResponse CheckinUser(CheckinUserRequest request)`

### POST `/api/OrderWorkflow/DeallocateOrderFromJob`

Remove order from a job

- `request`: 

`DeallocateOrderFromJobResponse DeallocateOrderFromJob(DeallocateOrderFromJobRequest request)`

### POST `/api/OrderWorkflow/GetGroup`

Get specific group by id. Detailed information about a group is returned

- `request`: 

`GetGroupResponse GetGroup(GetGroupRequest request)`

### POST `/api/OrderWorkflow/GetGroupList`

Returns a list of all groups in all workflows for all locations. The returned value does not contain group action and group conditions

- `request`: 

`GetGroupListResponse GetGroupList(GetGroupListRequest request)`

### POST `/api/OrderWorkflow/GetJob`

Get specific job details. This method will return list of order ids in a job.

- `request`: Request class

`GetJobResponse GetJob(GetJobRequest request)`

### POST `/api/OrderWorkflow/GetJobAudit`

Get job audit trail

- `request`: Request class

`GetJobAuditResponse GetJobAudit(GetJobAuditRequest request)`

### POST `/api/OrderWorkflow/GetJobByName`

Get specific job details. This method will return list of order ids in a job.

- `request`: Request class

`GetJobResponse GetJobByName(GetJobByNameRequest request)`

### POST `/api/OrderWorkflow/GetJobErrors`

Get job errors

- `request`: 

`GetJobErrorsResponse GetJobErrors(GetJobErrorsRequest request)`

### POST `/api/OrderWorkflow/GetPrintAttachment`

Marks the attachment as printed, reprints document if its not available anymore and returns the URL of the document to be downloaded

- `request`: 

`GetPrintAttachmentResponse GetPrintAttachment(GetPrintAttachmentRequest request)`

### POST `/api/OrderWorkflow/GetWorkflow`

Get workflow groups and jobs per location. This call will return all groups available to the user.  Group header only contains essential information for displaying the number of jobs to do, total number of orders in the group. Things like Conditions and Action lists are not returned as part of this call. Jobs - only header of a job is returned, actual list of order ids will be empty in this call. You will need to use GetJob to get actual list of orders allocated to the job

- `request`: Get workflow request

`GetWorkflowResponse GetWorkflow(GetWorkflowRequest request)`

### POST `/api/OrderWorkflow/Run`

Run the specified jobs

- `request`: Job id to run

`RunResponse Run(RunJobsRequest request)`

### POST `/api/OrderWorkflow/UpdateGroup`

Update group name, condition, list of actions

- `request`: Definition of updated fields of a specific group

`UpdateGroupResponse UpdateGroup(UpdateGroupRequest request)`
