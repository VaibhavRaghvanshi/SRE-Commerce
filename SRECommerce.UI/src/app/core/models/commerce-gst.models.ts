export interface GstParty { name: string; address: string; gstin: string | null; }
export type GstTaxTreatment = 'InterState' | 'IntraState' | 'IntraTerritory';
export interface GstTaxAmounts { cgst: number; sgst: number; utgst: number; igst: number; }
export interface GstWorksheetRequest {
  version: string;
  supplier: GstParty;
  recipient: GstParty;
  placeOfSupplyCode: string;
  placeOfSupplyName: string;
  reverseCharge: boolean;
  items: { variantId: string; hsn: string; unitCode: string }[];
  shippingSac: string | null;
  taxTreatment?: GstTaxTreatment | null;
}
export interface GstWorksheet {
  documentType: 'GST_WORKSHEET_DRAFT';
  orderNumber: string;
  orderDate: string;
  externalInvoiceReference: string | null;
  currency: string;
  supplier: GstParty;
  recipient: GstParty;
  deliveryAddress: string;
  placeOfSupplyCode: string;
  placeOfSupplyName: string;
  reverseCharge: boolean;
  lines: { sku: string; description: string; classification: string; unitCode: string; quantity: number; discount: number;
    taxableValue: number; recordedTaxRate: number; recordedTax: number; total: number; taxComponents?: GstTaxAmounts }[];
  shipping: { method: string; sac: string | null; taxableValue: number; recordedTax: number; total: number; taxComponents?: GstTaxAmounts };
  recordedTax: number;
  total: number;
  taxTreatment?: GstTaxTreatment;
  taxComponents?: GstTaxAmounts;
}

export interface GstInvoiceInput {
  number: string;
  issuedOn: string;
  evidenceReference: string;
  requiresIrn: boolean | null;
  irn: string | null;
  taxComponents: GstTaxAmounts;
  total: number | null;
  externallyIssued: boolean;
}

export interface GstInvoice {
  documentType: 'GST_EXTERNAL_INVOICE_RECORD';
  orderId: string;
  number: string;
  issuedOn: string;
  financialYearStart: number;
  evidenceReference: string;
  requiresIrn: boolean;
  irn: string | null;
  issuedTaxComponents: GstTaxAmounts;
  worksheet: GstWorksheet;
  recordedAt: string;
}
