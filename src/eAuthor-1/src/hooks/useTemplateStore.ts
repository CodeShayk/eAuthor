// Add property:
  baseDocxTemplateId?: string;
// In saveTemplate():
const data: any = {
  name: state.templateName,
  htmlBody: state.htmlBody,
  controls: state.controls,
  baseDocxTemplateId: state.baseDocxTemplateId
};