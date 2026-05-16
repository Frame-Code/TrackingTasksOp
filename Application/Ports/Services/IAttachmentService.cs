using Domain.Entities.OpenProjectEntities.WorkPackage;

namespace Application.Ports.Services;

public interface IAttachmentService
{
    Task<List<Attachment>> GetAttachmentsAsync(int workPackageId);
    Task<(byte[] Content, string ContentType, string FileName)> GetAttachmentContentAsync(int attachmentId);
}
