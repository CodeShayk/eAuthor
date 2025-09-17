export interface TemplateControlBinding {
  id: string;
  columnHeader: string;
  dataPath: string; // relative for repeater/grid
}

export interface TemplateControl {
  id: string;
  controlType: string;
  label?: string;
  dataPath: string;
  format?: string;
  optionsJson?: string;
  bindings?: TemplateControlBinding[];
  isRequired?: boolean;
  defaultValue?: string;
  width?: string;
  styleJson?: string;
}